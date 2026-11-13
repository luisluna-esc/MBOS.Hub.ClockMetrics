namespace ClockMetrics.Core.Entities.Attendance;

/// <summary>Grupo con el que se separan los reportes (MBOS, ENT, EDUCEN, SPA). El biométrico no lo trae.</summary>
public class WorkGroup
{
    public long WorkGroupId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
