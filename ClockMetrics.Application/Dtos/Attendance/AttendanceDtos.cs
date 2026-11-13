using ClockMetrics.Application.Attendance;

namespace ClockMetrics.Application.Dtos.Attendance;

/// <summary>Sumas de un empleado en un rango: las columnas del resumen mensual.</summary>
public class AttendanceTotalsDto
{
    public int WorkingDays { get; set; }
    public int CompleteDays { get; set; }
    /// <summary>Cantidad de atrasos (mañana y tarde cuentan por separado).</summary>
    public int LateCount { get; set; }
    public int LateMinutes { get; set; }
    public int LateAmMinutes { get; set; }
    public int LatePmMinutes { get; set; }
    public int MissingEntries { get; set; }
    public int MissingExits { get; set; }
    /// <summary>Días laborables sin ninguna marca.</summary>
    public int UnmarkedDays { get; set; }
    /// <summary>Mañanas o tardes sin ninguna marca en días que sí tienen otras marcas.</summary>
    public int UnmarkedHalfDays { get; set; }
    public int WorkedMinutes { get; set; }
    public int JustifiedDays { get; set; }
    /// <summary>Días con alguna incoherencia todavía sin revisar (sin ajuste).</summary>
    public int PendingIssueDays { get; set; }
}

public class PeriodDto
{
    public long AttendancePeriodId { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public bool IsClosed { get; set; }
    public DateTime? ClosedAt { get; set; }
    public int DayCount { get; set; }
}

public class EmployeeSummaryDto
{
    public long EmployeeId { get; set; }
    public string DeviceCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public long? WorkGroupId { get; set; }
    public string? WorkGroupName { get; set; }
}

/// <summary>Registro jornada laboral de un empleado en un período (lo que muestra el PDF por empleado).</summary>
public class EmployeeMonthDto
{
    public PeriodDto Period { get; set; } = new();
    public EmployeeSummaryDto Employee { get; set; } = new();
    public List<DayResult> Days { get; set; } = [];
    public AttendanceTotalsDto Totals { get; set; } = new();
}

public class SummaryRowDto
{
    public EmployeeSummaryDto Employee { get; set; } = new();
    public AttendanceTotalsDto Totals { get; set; } = new();
}

public class PeriodSummaryDto
{
    public PeriodDto Period { get; set; } = new();
    public List<SummaryRowDto> Rows { get; set; } = [];
}

public class IssueRowDto
{
    public EmployeeSummaryDto Employee { get; set; } = new();
    public DayResult Day { get; set; } = new();
}

public class AccumulatedRowDto
{
    public EmployeeSummaryDto Employee { get; set; } = new();
    /// <summary>Minutos de atraso de cada período, en el mismo orden que AccumulatedDto.Periods.</summary>
    public List<int> LateMinutesByPeriod { get; set; } = [];
    public AttendanceTotalsDto Totals { get; set; } = new();
}

public class AccumulatedDto
{
    public List<PeriodDto> Periods { get; set; } = [];
    public List<AccumulatedRowDto> Rows { get; set; } = [];
}

/// <summary>Cuerpo de PUT /api/attendance/employees/{id}/days/{date}: corregir o justificar un día.</summary>
public class AdjustmentRequestDto
{
    public bool OverrideMarks { get; set; }
    /// <summary>"HH:mm" o vacío.</summary>
    public string? EntryAm { get; set; }
    public string? ExitAm { get; set; }
    public string? EntryPm { get; set; }
    public string? ExitPm { get; set; }
    public string? JustificationType { get; set; }
    public bool ExcuseLateAm { get; set; }
    public bool ExcuseLatePm { get; set; }
    public bool ExcuseMissing { get; set; }
    public string Reason { get; set; } = string.Empty;
}

public class ImportResultDto
{
    public long AttendanceImportId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public DateOnly FirstDate { get; set; }
    public DateOnly LastDate { get; set; }
    public int EmployeeCount { get; set; }
    public int DayCount { get; set; }
    public int MarkCount { get; set; }
    /// <summary>Empleados que no existían (AC-No nuevo): hay que asignarles grupo.</summary>
    public List<string> NewEmployees { get; set; } = [];
    /// <summary>Empleados cuyo nombre en el biométrico cambió respecto del registrado.</summary>
    public List<string> RenamedEmployees { get; set; } = [];
    public List<PeriodDto> Periods { get; set; } = [];
    public List<string> Warnings { get; set; } = [];
}

public class AttendanceImportDto
{
    public long AttendanceImportId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public DateOnly FirstDate { get; set; }
    public DateOnly LastDate { get; set; }
    public int EmployeeCount { get; set; }
    public int NewEmployeeCount { get; set; }
    public int DayCount { get; set; }
    public int MarkCount { get; set; }
    public string? ImportedByName { get; set; }
    public DateTime ImportedAt { get; set; }
}
