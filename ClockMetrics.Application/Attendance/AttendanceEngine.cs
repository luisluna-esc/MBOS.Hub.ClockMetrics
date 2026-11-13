using ClockMetrics.Application.Dtos.Attendance;
using ClockMetrics.Core.Constants;
using ClockMetrics.Core.Entities.Attendance;
using ClockMetrics.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ClockMetrics.Application.Attendance;

/// <summary>Un empleado con sus días calculados en un rango de fechas.</summary>
public class EmployeeAttendance
{
    public Employee Employee { get; set; } = new();
    public string? WorkGroupName { get; set; }
    public int WorkGroupOrder { get; set; }
    public List<DayResult> Days { get; set; } = [];
    public AttendanceTotalsDto Totals { get; set; } = new();

    public EmployeeSummaryDto ToSummary() => new()
    {
        EmployeeId = Employee.EmployeeId,
        DeviceCode = Employee.DeviceCode,
        FullName = Employee.FullName,
        WorkGroupId = Employee.WorkGroupId,
        WorkGroupName = WorkGroupName
    };
}

// Carga todo lo que necesita el cálculo (reglas, horario, feriados, marcas y ajustes) en pocas
// consultas y arma los días con AttendanceCalculator. Lo usan la pantalla y los reportes, así los
// dos muestran siempre lo mismo. No guarda nada: los días se calculan cada vez, así un cambio de
// regla o un ajuste se ve al instante.
public class AttendanceEngine
{
    private readonly IUnitOfWork _unitOfWork;

    public AttendanceEngine(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<AttendanceSetting> GetSettingsAsync() =>
        await _unitOfWork.Repository<AttendanceSetting>().Query().FirstOrDefaultAsync() ?? new AttendanceSetting { AttendanceSettingId = 1 };

    /// <summary>Empleados del filtro, ordenados por grupo y nombre (como los reportes).</summary>
    public async Task<List<EmployeeAttendance>> CalculateAsync(DateOnly from, DateOnly to, long? workGroupId, long? employeeId, bool onlyIncludedInReports)
    {
        var employeesQuery = _unitOfWork.Repository<Employee>().Query();
        if (employeeId.HasValue)
            employeesQuery = employeesQuery.Where(e => e.EmployeeId == employeeId.Value);
        else
        {
            if (onlyIncludedInReports)
                employeesQuery = employeesQuery.Where(e => e.IncludeInReports && e.IsActive);
            if (workGroupId.HasValue)
                employeesQuery = employeesQuery.Where(e => e.WorkGroupId == workGroupId.Value);
        }

        var employees = await employeesQuery.ToListAsync();
        if (employees.Count == 0)
            return [];

        var employeeIds = employees.Select(e => e.EmployeeId).ToList();
        var groups = await _unitOfWork.Repository<WorkGroup>().Query().ToDictionaryAsync(g => g.WorkGroupId);
        var settings = await GetSettingsAsync();
        var schedules = await _unitOfWork.Repository<WorkSchedule>().Query().ToDictionaryAsync(s => s.DayOfWeek);
        var holidays = await _unitOfWork.Repository<Holiday>().Query()
            .Where(h => h.Date >= from && h.Date <= to)
            .ToDictionaryAsync(h => h.Date, h => h.Name);
        var rawDays = await _unitOfWork.Repository<AttendanceRawDay>().Query()
            .Where(r => employeeIds.Contains(r.EmployeeId) && r.WorkDate >= from && r.WorkDate <= to)
            .ToDictionaryAsync(r => (r.EmployeeId, r.WorkDate), r => r.Marks);
        var adjustments = await _unitOfWork.Repository<AttendanceAdjustment>().Query()
            .Where(a => employeeIds.Contains(a.EmployeeId) && a.WorkDate >= from && a.WorkDate <= to)
            .ToDictionaryAsync(a => (a.EmployeeId, a.WorkDate));

        var result = new List<EmployeeAttendance>();
        foreach (var employee in employees)
        {
            var group = employee.WorkGroupId is { } gid && groups.TryGetValue(gid, out var g) ? g : null;
            var attendance = new EmployeeAttendance
            {
                Employee = employee,
                WorkGroupName = group?.Name,
                WorkGroupOrder = group?.DisplayOrder ?? int.MaxValue
            };

            for (var date = from; date <= to; date = date.AddDays(1))
            {
                var dayOfWeek = date.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)date.DayOfWeek;
                attendance.Days.Add(AttendanceCalculator.Calculate(
                    date,
                    rawDays.GetValueOrDefault((employee.EmployeeId, date), string.Empty),
                    schedules.GetValueOrDefault(dayOfWeek),
                    holidays.GetValueOrDefault(date),
                    settings,
                    adjustments.GetValueOrDefault((employee.EmployeeId, date))));
            }

            attendance.Totals = Sum(attendance.Days);
            result.Add(attendance);
        }

        return result
            .OrderBy(a => a.WorkGroupOrder)
            .ThenBy(a => a.Employee.FullName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    public static AttendanceTotalsDto Sum(IEnumerable<DayResult> days)
    {
        var totals = new AttendanceTotalsDto();
        foreach (var day in days.Where(d => d.IsWorkingDay))
        {
            totals.WorkingDays++;
            if (day.Status == DayStatuses.Complete) totals.CompleteDays++;
            if (day.Status == DayStatuses.Justified || day.JustificationType is not null) totals.JustifiedDays++;
            totals.LateCount += day.LateCount;
            totals.LateMinutes += day.LateMinutes;
            totals.LateAmMinutes += day.LateAmMinutes;
            totals.LatePmMinutes += day.LatePmMinutes;
            totals.MissingEntries += day.MissingEntries;
            totals.MissingExits += day.MissingExits;
            if (day.UnmarkedHalves >= 2) totals.UnmarkedDays++;
            else totals.UnmarkedHalfDays += day.UnmarkedHalves;
            totals.WorkedMinutes += day.WorkedMinutes ?? 0;
            if (day.Issues.Count > 0 && !day.IsAdjusted) totals.PendingIssueDays++;
        }
        return totals;
    }

    public static PeriodDto ToDto(AttendancePeriod period) => new()
    {
        AttendancePeriodId = period.AttendancePeriodId,
        Year = period.Year,
        Month = period.Month,
        StartDate = period.StartDate,
        EndDate = period.EndDate,
        IsClosed = period.IsClosed,
        ClosedAt = period.ClosedAt,
        DayCount = period.EndDate.DayNumber - period.StartDate.DayNumber + 1
    };

    /// <summary>Período al que pertenece una fecha: con corte 20, el 21/07 ya es de agosto.</summary>
    public static (int Year, int Month) PeriodOf(DateOnly date, int cutoffDay)
    {
        var month = new DateOnly(date.Year, date.Month, 1);
        if (date.Day > Math.Min(cutoffDay, DateTime.DaysInMonth(date.Year, date.Month)))
            month = month.AddMonths(1);
        return (month.Year, month.Month);
    }

    /// <summary>Fechas de un período: del día siguiente al corte del mes anterior hasta el corte del mes.</summary>
    public static (DateOnly Start, DateOnly End) RangeOf(int year, int month, int cutoffDay)
    {
        var end = new DateOnly(year, month, Math.Min(cutoffDay, DateTime.DaysInMonth(year, month)));
        var previous = new DateOnly(year, month, 1).AddMonths(-1);
        var start = new DateOnly(previous.Year, previous.Month, Math.Min(cutoffDay, DateTime.DaysInMonth(previous.Year, previous.Month))).AddDays(1);
        return (start, end);
    }
}
