namespace ClockMetrics.Core.Entities.Attendance;

/// <summary>Mes de control (ej. agosto 2026 = 21/07 al 20/08). Cerrado ya no admite importaciones ni ajustes.</summary>
public class AttendancePeriod
{
    public long AttendancePeriodId { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public bool IsClosed { get; set; }
    public DateTime? ClosedAt { get; set; }
    public long? ClosedById { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
