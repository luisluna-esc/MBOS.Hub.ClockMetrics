namespace ClockMetrics.Core.QueryFilters.Attendance;

public class EmployeeQueryFilter
{
    public long? WorkGroupId { get; set; }
    public bool? IncludeInReports { get; set; }
    public bool? IsActive { get; set; }
}
