namespace ClockMetrics.Core.Constants;

/// <summary>Estado de un día calculado.</summary>
public static class DayStatuses
{
    public const string NonWorking = "NO_LABORAL";
    public const string Holiday = "FERIADO";
    public const string NoMarks = "SIN_MARCAS";
    public const string Incomplete = "INCOMPLETO";
    public const string Complete = "COMPLETO";
    /// <summary>Día sin marcas o incompleto, pero justificado.</summary>
    public const string Justified = "JUSTIFICADO";
}

/// <summary>Incoherencias que el cálculo encuentra y que RR.HH. debería revisar.</summary>
public static class DayIssues
{
    /// <summary>Se presionó dos veces seguidas: se tomó la primera marca.</summary>
    public const string Duplicate = "DUPLICADA";
    /// <summary>Más marcas de las que entran en las casillas del día: alguna no se usó.</summary>
    public const string ExtraMarks = "MARCAS_EXTRA";
    public const string MissingMark = "FALTA_MARCA";
    public const string ShortBreak = "RECESO_CORTO";
    public const string OddHour = "HORA_INUSUAL";
    public const string NoMarks = "SIN_MARCAS";
    /// <summary>Hay marcas en un fin de semana o feriado.</summary>
    public const string MarksOnNonWorkingDay = "MARCAS_DIA_NO_LABORAL";
}
