namespace ClockMetrics.Application.Attendance;

/// <summary>Un día de un empleado ya calculado: lo que muestra el informe y lo que suman los resúmenes.</summary>
public class DayResult
{
    public DateOnly Date { get; set; }
    /// <summary>1 = lunes ... 7 = domingo.</summary>
    public int DayOfWeek { get; set; }
    /// <summary>Uno de DayStatuses.</summary>
    public string Status { get; set; } = string.Empty;
    public string? HolidayName { get; set; }
    public bool IsContinuous { get; set; }

    /// <summary>Marcas tal como vinieron del biométrico.</summary>
    public string RawMarks { get; set; } = string.Empty;
    public TimeOnly? EntryAm { get; set; }
    public TimeOnly? ExitAm { get; set; }
    public TimeOnly? EntryPm { get; set; }
    public TimeOnly? ExitPm { get; set; }

    public int LateAmMinutes { get; set; }
    public int LatePmMinutes { get; set; }
    public int LateMinutes => LateAmMinutes + LatePmMinutes;
    /// <summary>Atrasos del día (mañana y tarde cuentan por separado), como la columna del resumen.</summary>
    public int LateCount => (LateAmMinutes > 0 ? 1 : 0) + (LatePmMinutes > 0 ? 1 : 0);
    /// <summary>Solo con entrada y salida: salida − entrada − receso del horario.</summary>
    public int? WorkedMinutes { get; set; }

    public int MissingEntries { get; set; }
    public int MissingExits { get; set; }
    /// <summary>Medios días sin ninguna marca (2 = el día entero).</summary>
    public int UnmarkedHalves { get; set; }

    /// <summary>Incoherencias para revisar (DayIssues).</summary>
    public List<string> Issues { get; set; } = [];

    public bool IsAdjusted { get; set; }
    public bool OverrideMarks { get; set; }
    public string? JustificationType { get; set; }
    public bool ExcuseLateAm { get; set; }
    public bool ExcuseLatePm { get; set; }
    public bool ExcuseMissing { get; set; }
    public string? Reason { get; set; }

    public bool IsWorkingDay => Status is not (Core.Constants.DayStatuses.NonWorking or Core.Constants.DayStatuses.Holiday);
}
