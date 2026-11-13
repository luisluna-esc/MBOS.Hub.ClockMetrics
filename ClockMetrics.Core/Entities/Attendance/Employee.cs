namespace ClockMetrics.Core.Entities.Attendance;

public class Employee
{
    public long EmployeeId { get; set; }
    /// <summary>AC-No del biométrico: es lo que identifica a la persona al importar (el nombre puede variar).</summary>
    public string DeviceCode { get; set; } = string.Empty;
    /// <summary>Nombre con el que sale en los reportes; se puede corregir sin tocar el biométrico.</summary>
    public string FullName { get; set; } = string.Empty;
    /// <summary>Nombre tal como vino del biométrico en la última importación.</summary>
    public string DeviceName { get; set; } = string.Empty;
    public long? WorkGroupId { get; set; }
    /// <summary>Si sale en el informe por empleado y en los resúmenes (el reloj tiene más gente de la que se reporta).</summary>
    public bool IncludeInReports { get; set; } = true;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
