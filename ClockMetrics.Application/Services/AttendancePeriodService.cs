using System.Net;
using ClockMetrics.Application.Attendance;
using ClockMetrics.Application.Dtos.Attendance;
using ClockMetrics.Application.Interfaces.Attendance;
using ClockMetrics.Application.Interfaces.Security;
using ClockMetrics.Core.Entities.Attendance;
using ClockMetrics.Core.Entities.ResponseApi.DisplayFormat;
using ClockMetrics.Core.Interfaces;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace ClockMetrics.Application.Services;

// Los períodos los crea la importación (21 al 20). Aquí se pueden corregir sus fechas (el de febrero
// 2026 empezó el 01/02) y cerrarlos: cerrado, ya no se importa ni se ajusta nada de esas fechas.
public class AttendancePeriodService : IAttendancePeriodService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly IValidator<PeriodRequestDto> _validator;

    public AttendancePeriodService(IUnitOfWork unitOfWork, ICurrentUserService currentUserService, IValidator<PeriodRequestDto> validator)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _validator = validator;
    }

    public async Task<ResponseGetObject> GetAll(int? year)
    {
        var query = _unitOfWork.Repository<AttendancePeriod>().Query();
        if (year.HasValue)
            query = query.Where(p => p.Year == year.Value);

        var periods = await query.OrderByDescending(p => p.StartDate).ToListAsync();
        return Responses.Ok(periods.Select(AttendanceEngine.ToDto).ToList());
    }

    public async Task<ResponsePost> Insert(PeriodRequestDto request)
    {
        var errors = await Validate(request, null);
        if (errors.Length > 0)
            return Responses.PostError(HttpStatusCode.BadRequest, errors);

        var period = new AttendancePeriod { Year = request.Year, Month = request.Month, StartDate = request.StartDate, EndDate = request.EndDate, CreatedAt = DateTime.UtcNow };
        await _unitOfWork.Repository<AttendancePeriod>().AddAsync(period);
        await _unitOfWork.SaveChangesAsync();
        return Responses.Saved(period.AttendancePeriodId, "Período creado.", HttpStatusCode.Created);
    }

    public async Task<ResponsePost> Update(long id, PeriodRequestDto request)
    {
        var repository = _unitOfWork.Repository<AttendancePeriod>();
        var period = await repository.GetByIdAsync(id);
        if (period is null)
            return Responses.PostError(HttpStatusCode.NotFound, "No se encontró el período.");
        if (period.IsClosed)
            return Responses.PostError(HttpStatusCode.Conflict, "El período está cerrado. Reábrelo para cambiar sus fechas.");

        var errors = await Validate(request, id);
        if (errors.Length > 0)
            return Responses.PostError(HttpStatusCode.BadRequest, errors);

        period.Year = request.Year;
        period.Month = request.Month;
        period.StartDate = request.StartDate;
        period.EndDate = request.EndDate;
        repository.Update(period);
        await _unitOfWork.SaveChangesAsync();
        return Responses.Saved(id, "Período actualizado.");
    }

    public async Task<ResponsePost> SetClosed(long id, bool closed)
    {
        var repository = _unitOfWork.Repository<AttendancePeriod>();
        var period = await repository.GetByIdAsync(id);
        if (period is null)
            return Responses.PostError(HttpStatusCode.NotFound, "No se encontró el período.");

        period.IsClosed = closed;
        period.ClosedAt = closed ? DateTime.UtcNow : null;
        period.ClosedById = closed ? _currentUserService.AppUserId : null;
        repository.Update(period);
        await _unitOfWork.SaveChangesAsync();
        return Responses.Saved(id, closed ? "Período cerrado." : "Período reabierto.");
    }

    private async Task<string[]> Validate(PeriodRequestDto request, long? id)
    {
        var validation = await _validator.ValidateAsync(request);
        var errors = validation.Errors.Select(e => e.ErrorMessage).ToList();
        var query = _unitOfWork.Repository<AttendancePeriod>().Query().Where(p => p.AttendancePeriodId != id);
        if (await query.AnyAsync(p => p.Year == request.Year && p.Month == request.Month))
            errors.Add("Ya existe ese período.");
        if (await query.AnyAsync(p => p.StartDate <= request.EndDate && p.EndDate >= request.StartDate))
            errors.Add("Las fechas se cruzan con otro período.");
        return errors.ToArray();
    }
}
