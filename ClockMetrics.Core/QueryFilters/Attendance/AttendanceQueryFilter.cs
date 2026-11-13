namespace ClockMetrics.Core.QueryFilters.Attendance;

/// <summary>Filtro de las consultas de asistencia de un período.</summary>
public class AttendanceQueryFilter
{
    public long? WorkGroupId { get; set; }
    public long? EmployeeId { get; set; }
    /// <summary>Por defecto solo los empleados marcados para reportes.</summary>
    public bool OnlyIncludedInReports { get; set; } = true;
}
