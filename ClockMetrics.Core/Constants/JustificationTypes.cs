namespace ClockMetrics.Core.Constants;

public static class JustificationTypes
{
    public const string Permission = "PERMISO";
    public const string Commission = "COMISION";
    public const string MedicalLeave = "BAJA_MEDICA";
    public const string Vacation = "VACACION";
    public const string ForgotToMark = "OLVIDO_MARCAR";
    public const string DeviceFailure = "FALLA_BIOMETRICO";
    public const string Other = "OTRO";

    public static readonly string[] All = [Permission, Commission, MedicalLeave, Vacation, ForgotToMark, DeviceFailure, Other];
}
