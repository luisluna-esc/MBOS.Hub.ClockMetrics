namespace ClockMetrics.Core.Entities.Attendance;

/// <summary>Cada archivo del biométrico que se subió.</summary>
public class AttendanceImport
{
    public long AttendanceImportId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public DateOnly FirstDate { get; set; }
    public DateOnly LastDate { get; set; }
    public int EmployeeCount { get; set; }
    public int NewEmployeeCount { get; set; }
    public int DayCount { get; set; }
    public int MarkCount { get; set; }
    public long? ImportedById { get; set; }
    public DateTime ImportedAt { get; set; } = DateTime.UtcNow;
}
