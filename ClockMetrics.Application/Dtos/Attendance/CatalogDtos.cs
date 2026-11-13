namespace ClockMetrics.Application.Dtos.Attendance;

public class EmployeeDto
{
    public long EmployeeId { get; set; }
    public string DeviceCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string DeviceName { get; set; } = string.Empty;
    public long? WorkGroupId { get; set; }
    public string? WorkGroupName { get; set; }
    public bool IncludeInReports { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>Lo que se puede cambiar de un empleado: el AC-No y el nombre del biométrico no.</summary>
public class EmployeeRequestDto
{
    public string FullName { get; set; } = string.Empty;
    public long? WorkGroupId { get; set; }
    public bool IncludeInReports { get; set; }
    public bool IsActive { get; set; }
}

public class WorkGroupDto
{
    public long WorkGroupId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; }
    public int EmployeeCount { get; set; }
}

public class HolidayDto
{
    public long HolidayId { get; set; }
    public DateOnly Date { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class PeriodRequestDto
{
    public int Year { get; set; }
    public int Month { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
}

public class WorkScheduleDto
{
    public int DayOfWeek { get; set; }
    public bool IsWorkingDay { get; set; }
    /// <summary>"HH:mm" o null.</summary>
    public string? StartTime { get; set; }
    public string? BreakStart { get; set; }
    public string? BreakEnd { get; set; }
    public string? EndTime { get; set; }
}

public class AttendanceSettingsDto
{
    public int ToleranceMinutes { get; set; }
    public int DuplicateWindowMinutes { get; set; }
    public int MinBreakMinutes { get; set; }
    public string MiddayFrom { get; set; } = "12:00";
    public string AfternoonExitFrom { get; set; } = "16:00";
    public string EarliestNormalTime { get; set; } = "06:00";
    public string LatestNormalTime { get; set; } = "22:00";
    public int PeriodCutoffDay { get; set; }
    public List<WorkScheduleDto> Schedules { get; set; } = [];
}
