namespace ClockMetrics.Core.Entities.Attendance;

public class Holiday
{
    public long HolidayId { get; set; }
    public DateOnly Date { get; set; }
    public string Name { get; set; } = string.Empty;
}
