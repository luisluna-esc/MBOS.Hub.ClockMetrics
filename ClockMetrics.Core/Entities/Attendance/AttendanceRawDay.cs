namespace ClockMetrics.Core.Entities.Attendance;

/// <summary>
/// Las marcas de un empleado en un día, tal como vienen del biométrico ("08:01 13:37 13:37 18:27").
/// Nunca se editan a mano: las correcciones van en AttendanceAdjustment, así el dato original queda.
/// </summary>
public class AttendanceRawDay
{
    public long AttendanceRawDayId { get; set; }
    public long EmployeeId { get; set; }
    public DateOnly WorkDate { get; set; }
    public string Marks { get; set; } = string.Empty;
    public long AttendanceImportId { get; set; }
}
