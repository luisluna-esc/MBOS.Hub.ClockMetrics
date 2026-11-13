namespace ClockMetrics.Core.Entities.Attendance;

/// <summary>
/// Corrección o justificación de un día, hecha por RR.HH. con su motivo (queda en la auditoría).
/// Con OverrideMarks las cuatro horas de aquí reemplazan a las del biométrico; los Excuse* hacen
/// que ese atraso o esa falta de marca no cuente en los resúmenes.
/// </summary>
public class AttendanceAdjustment
{
    public long AttendanceAdjustmentId { get; set; }
    public long EmployeeId { get; set; }
    public DateOnly WorkDate { get; set; }
    public bool OverrideMarks { get; set; }
    public TimeOnly? EntryAm { get; set; }
    public TimeOnly? ExitAm { get; set; }
    public TimeOnly? EntryPm { get; set; }
    public TimeOnly? ExitPm { get; set; }
    /// <summary>Uno de JustificationTypes, o null si solo se corrigieron las marcas.</summary>
    public string? JustificationType { get; set; }
    public bool ExcuseLateAm { get; set; }
    public bool ExcuseLatePm { get; set; }
    /// <summary>No cuentan las marcas que faltan ni el día o medio día sin marcar.</summary>
    public bool ExcuseMissing { get; set; }
    public string Reason { get; set; } = string.Empty;
    public long? CreatedById { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public long? UpdatedById { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
