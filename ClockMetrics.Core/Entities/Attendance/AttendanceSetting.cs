namespace ClockMetrics.Core.Entities.Attendance;

/// <summary>Reglas del cálculo de asistencia. Hay una sola fila (id 1).</summary>
public class AttendanceSetting
{
    public long AttendanceSettingId { get; set; }
    /// <summary>Minutos de gracia: hasta la hora de entrada más esto no es atraso; pasado eso se cuenta desde la hora de entrada.</summary>
    public int ToleranceMinutes { get; set; } = 5;
    /// <summary>Dos marcas a menos de estos minutos son la misma (se presionó dos veces): se queda la primera.</summary>
    public int DuplicateWindowMinutes { get; set; } = 10;
    /// <summary>Un receso más corto que esto se marca para revisar.</summary>
    public int MinBreakMinutes { get; set; } = 30;
    /// <summary>Desde esta hora una marca ya no es la entrada de la mañana sino la salida al receso o el regreso.</summary>
    public TimeOnly MiddayFrom { get; set; } = new(12, 0);
    /// <summary>Desde esta hora una marca es la salida de la tarde.</summary>
    public TimeOnly AfternoonExitFrom { get; set; } = new(16, 0);
    /// <summary>Marcas antes de esta hora o después de LatestNormalTime se marcan para revisar.</summary>
    public TimeOnly EarliestNormalTime { get; set; } = new(6, 0);
    public TimeOnly LatestNormalTime { get; set; } = new(22, 0);
    /// <summary>Último día de cada período: con 20, agosto va del 21 de julio al 20 de agosto.</summary>
    public int PeriodCutoffDay { get; set; } = 20;
}
