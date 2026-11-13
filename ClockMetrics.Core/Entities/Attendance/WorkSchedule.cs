namespace ClockMetrics.Core.Entities.Attendance;

/// <summary>
/// Horario de un día de la semana. Sin receso (BreakStart/BreakEnd en null) es jornada continua,
/// como el viernes: solo cuentan la entrada de la mañana y la salida de la tarde.
/// </summary>
public class WorkSchedule
{
    public long WorkScheduleId { get; set; }
    /// <summary>1 = lunes ... 7 = domingo (ISO).</summary>
    public int DayOfWeek { get; set; }
    public bool IsWorkingDay { get; set; }
    public TimeOnly? StartTime { get; set; }
    public TimeOnly? BreakStart { get; set; }
    public TimeOnly? BreakEnd { get; set; }
    public TimeOnly? EndTime { get; set; }

    public bool IsContinuous => BreakStart is null || BreakEnd is null;
}
