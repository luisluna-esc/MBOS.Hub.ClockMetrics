namespace ClockMetrics.Core.Constants;

public static class RoleNames
{
    /// <summary>Superusuario: el único rol que administra usuarios, roles y menús.</summary>
    public const string Admin = "M-BOS";
    /// <summary>Recursos Humanos: importa el biométrico, corrige y justifica, cierra períodos.</summary>
    public const string HumanResources = "RRHH";
    /// <summary>Solo consulta y descarga reportes.</summary>
    public const string Viewer = "Consulta";

    /// <summary>Roles que registran y modifican datos; el resto solo visualiza.</summary>
    public const string Writers = Admin + "," + HumanResources;

    /// <summary>Roles que pueden consultar la auditoría.</summary>
    public const string AuditViewers = Admin + "," + HumanResources;
}
