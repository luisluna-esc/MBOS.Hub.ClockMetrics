using System.Net;
using ClockMetrics.Application.Attendance;
using ClockMetrics.Application.Dtos.Attendance;
using ClockMetrics.Application.Interfaces.Attendance;
using ClockMetrics.Application.Interfaces.Security;
using ClockMetrics.Core.Constants;
using ClockMetrics.Core.Entities.Attendance;
using ClockMetrics.Core.Entities.ResponseApi.DisplayFormat;
using ClockMetrics.Core.Interfaces;
using ClockMetrics.Core.QueryFilters.Attendance;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace ClockMetrics.Application.Services;

public class AttendanceService : IAttendanceService
{
    private const int MaxAccumulatedPeriods = 24;

    private readonly IUnitOfWork _unitOfWork;
    private readonly AttendanceEngine _engine;
    private readonly ICurrentUserService _currentUserService;
    private readonly IValidator<AdjustmentRequestDto> _adjustmentValidator;

    public AttendanceService(IUnitOfWork unitOfWork, AttendanceEngine engine, ICurrentUserService currentUserService, IValidator<AdjustmentRequestDto> adjustmentValidator)
    {
        _unitOfWork = unitOfWork;
        _engine = engine;
        _currentUserService = currentUserService;
        _adjustmentValidator = adjustmentValidator;
    }

    public async Task<ResponseGetObject> GetEmployeeMonth(long periodId, long employeeId)
    {
        var period = await _unitOfWork.Repository<AttendancePeriod>().GetByIdAsync(periodId);
        if (period is null)
            return Responses.GetError(HttpStatusCode.NotFound, "No se encontró el período.");

        var attendance = (await _engine.CalculateAsync(period.StartDate, period.EndDate, null, employeeId, false)).FirstOrDefault();
        if (attendance is null)
            return Responses.GetError(HttpStatusCode.NotFound, "No se encontró el empleado.");

        return Responses.Ok(new EmployeeMonthDto
        {
            Period = AttendanceEngine.ToDto(period),
            Employee = attendance.ToSummary(),
            Days = attendance.Days,
            Totals = attendance.Totals
        });
    }

    /// <summary>Registro jornada laboral de todos los empleados del filtro (la vista general).</summary>
    public async Task<ResponseGetObject> GetRecords(long periodId, AttendanceQueryFilter filter)
    {
        var period = await _unitOfWork.Repository<AttendancePeriod>().GetByIdAsync(periodId);
        if (period is null)
            return Responses.GetError(HttpStatusCode.NotFound, "No se encontró el período.");

        var periodDto = AttendanceEngine.ToDto(period);
        var rows = await _engine.CalculateAsync(period.StartDate, period.EndDate, filter.WorkGroupId, filter.EmployeeId, filter.OnlyIncludedInReports);
        return Responses.Ok(rows.Select(r => new EmployeeMonthDto
        {
            Period = periodDto,
            Employee = r.ToSummary(),
            Days = r.Days,
            Totals = r.Totals
        }).ToList());
    }

    public async Task<ResponseGetObject> GetSummary(long periodId, AttendanceQueryFilter filter)
    {
        var period = await _unitOfWork.Repository<AttendancePeriod>().GetByIdAsync(periodId);
        if (period is null)
            return Responses.GetError(HttpStatusCode.NotFound, "No se encontró el período.");

        var rows = await _engine.CalculateAsync(period.StartDate, period.EndDate, filter.WorkGroupId, filter.EmployeeId, filter.OnlyIncludedInReports);
        return Responses.Ok(new PeriodSummaryDto
        {
            Period = AttendanceEngine.ToDto(period),
            Rows = rows.Select(r => new SummaryRowDto { Employee = r.ToSummary(), Totals = r.Totals }).ToList()
        });
    }

    public async Task<ResponseGetObject> GetIssues(long periodId, AttendanceQueryFilter filter, bool includeReviewed)
    {
        var period = await _unitOfWork.Repository<AttendancePeriod>().GetByIdAsync(periodId);
        if (period is null)
            return Responses.GetError(HttpStatusCode.NotFound, "No se encontró el período.");

        var rows = await _engine.CalculateAsync(period.StartDate, period.EndDate, filter.WorkGroupId, filter.EmployeeId, filter.OnlyIncludedInReports);
        var issues = rows
            .SelectMany(r => r.Days
                .Where(d => d.Issues.Count > 0 && (includeReviewed || !d.IsAdjusted))
                .Select(d => new IssueRowDto { Employee = r.ToSummary(), Day = d }))
            .OrderBy(i => i.Day.Date)
            .ThenBy(i => i.Employee.FullName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
        return Responses.Ok(issues);
    }

    public async Task<ResponseGetObject> GetAccumulated(long fromPeriodId, long toPeriodId, AttendanceQueryFilter filter)
    {
        var periods = await LoadPeriodRange(fromPeriodId, toPeriodId);
        if (periods is null)
            return Responses.GetError(HttpStatusCode.BadRequest, $"Elige un período inicial y uno final válidos (hasta {MaxAccumulatedPeriods} meses).");

        return Responses.Ok(await BuildAccumulated(periods, filter));
    }

    /// <summary>Períodos entre dos (incluidos), en orden; null si el rango no es válido.</summary>
    public async Task<List<AttendancePeriod>?> LoadPeriodRange(long fromPeriodId, long toPeriodId)
    {
        var repository = _unitOfWork.Repository<AttendancePeriod>();
        var from = await repository.GetByIdAsync(fromPeriodId);
        var to = await repository.GetByIdAsync(toPeriodId);
        if (from is null || to is null || from.StartDate > to.StartDate)
            return null;

        var periods = await repository.Query()
            .Where(p => p.StartDate >= from.StartDate && p.StartDate <= to.StartDate)
            .OrderBy(p => p.StartDate)
            .ToListAsync();
        return periods.Count is > 0 and <= MaxAccumulatedPeriods ? periods : null;
    }

    public async Task<AccumulatedDto> BuildAccumulated(List<AttendancePeriod> periods, AttendanceQueryFilter filter)
    {
        var all = await _engine.CalculateAsync(periods[0].StartDate, periods[^1].EndDate, filter.WorkGroupId, filter.EmployeeId, filter.OnlyIncludedInReports);
        var rows = all.Select(a => new AccumulatedRowDto
        {
            Employee = a.ToSummary(),
            LateMinutesByPeriod = periods
                .Select(p => a.Days.Where(d => d.Date >= p.StartDate && d.Date <= p.EndDate).Sum(d => d.LateMinutes))
                .ToList(),
            Totals = AttendanceEngine.Sum(a.Days.Where(d => periods.Any(p => d.Date >= p.StartDate && d.Date <= p.EndDate)))
        })
        // Como la hoja RESUMEN: de más a menos atrasado.
        .OrderByDescending(r => r.Totals.LateMinutes)
        .ThenBy(r => r.Employee.FullName, StringComparer.CurrentCultureIgnoreCase)
        .ToList();

        return new AccumulatedDto { Periods = periods.Select(AttendanceEngine.ToDto).ToList(), Rows = rows };
    }

    public async Task<ResponsePost> SaveAdjustment(long employeeId, DateOnly date, AdjustmentRequestDto request)
    {
        var validation = await _adjustmentValidator.ValidateAsync(request);
        if (!validation.IsValid)
            return Responses.PostError(HttpStatusCode.BadRequest, validation.Errors.Select(e => e.ErrorMessage).ToArray());

        if (await _unitOfWork.Repository<Employee>().GetByIdAsync(employeeId) is null)
            return Responses.PostError(HttpStatusCode.NotFound, "No se encontró el empleado.");
        if (await IsInClosedPeriod(date))
            return Responses.PostError(HttpStatusCode.Conflict, "El período de esa fecha está cerrado. Reábrelo para hacer cambios.");

        var repository = _unitOfWork.Repository<AttendanceAdjustment>();
        var existing = await repository.Query().FirstOrDefaultAsync(a => a.EmployeeId == employeeId && a.WorkDate == date);
        var adjustment = existing is null ? new AttendanceAdjustment { EmployeeId = employeeId, WorkDate = date } : (await repository.GetByIdAsync(existing.AttendanceAdjustmentId))!;

        adjustment.OverrideMarks = request.OverrideMarks;
        adjustment.EntryAm = request.OverrideMarks ? ParseTime(request.EntryAm) : null;
        adjustment.ExitAm = request.OverrideMarks ? ParseTime(request.ExitAm) : null;
        adjustment.EntryPm = request.OverrideMarks ? ParseTime(request.EntryPm) : null;
        adjustment.ExitPm = request.OverrideMarks ? ParseTime(request.ExitPm) : null;
        adjustment.JustificationType = string.IsNullOrWhiteSpace(request.JustificationType) ? null : request.JustificationType;
        adjustment.ExcuseLateAm = request.ExcuseLateAm;
        adjustment.ExcuseLatePm = request.ExcuseLatePm;
        adjustment.ExcuseMissing = request.ExcuseMissing;
        adjustment.Reason = request.Reason.Trim();

        if (existing is null)
        {
            adjustment.CreatedById = _currentUserService.AppUserId;
            adjustment.CreatedAt = DateTime.UtcNow;
            await repository.AddAsync(adjustment);
        }
        else
        {
            adjustment.UpdatedById = _currentUserService.AppUserId;
            adjustment.UpdatedAt = DateTime.UtcNow;
            repository.Update(adjustment);
        }

        await _unitOfWork.SaveChangesAsync();
        return Responses.Saved(adjustment.AttendanceAdjustmentId, "Día actualizado.");
    }

    public async Task<ResponsePost> DeleteAdjustment(long employeeId, DateOnly date)
    {
        var repository = _unitOfWork.Repository<AttendanceAdjustment>();
        var existing = await repository.Query().FirstOrDefaultAsync(a => a.EmployeeId == employeeId && a.WorkDate == date);
        if (existing is null)
            return Responses.PostError(HttpStatusCode.NotFound, "Ese día no tiene ajustes.");
        if (await IsInClosedPeriod(date))
            return Responses.PostError(HttpStatusCode.Conflict, "El período de esa fecha está cerrado. Reábrelo para hacer cambios.");

        repository.Remove((await repository.GetByIdAsync(existing.AttendanceAdjustmentId))!);
        await _unitOfWork.SaveChangesAsync();
        return Responses.Saved(existing.AttendanceAdjustmentId, "Se quitó el ajuste: el día vuelve a las marcas del biométrico.");
    }

    private Task<bool> IsInClosedPeriod(DateOnly date) =>
        _unitOfWork.Repository<AttendancePeriod>().Query().AnyAsync(p => p.IsClosed && p.StartDate <= date && p.EndDate >= date);

    public static TimeOnly? ParseTime(string? value) =>
        TimeOnly.TryParseExact(value?.Trim(), ["HH:mm", "H:mm", "HH:mm:ss"], out var time) ? new TimeOnly(time.Hour, time.Minute) : null;
}
