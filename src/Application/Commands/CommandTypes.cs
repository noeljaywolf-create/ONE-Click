namespace OneClick.Application.Commands;

/// <summary>
/// Stable command type identifiers for the initial typed command union.
/// </summary>
public static class CommandTypes
{
    public const string ImportDocument = "ImportDocument";
    public const string SetCalibration = "SetCalibration";
    public const string UpsertRoute = "UpsertRoute";
    public const string PlaceComponent = "PlaceComponent";
    public const string ConnectPorts = "ConnectPorts";
    public const string AcceptProposal = "AcceptProposal";
    public const string ReviewProperty = "ReviewProperty";
    public const string SelectAssembly = "SelectAssembly";
    public const string ReleaseSnapshot = "ReleaseSnapshot";
}
