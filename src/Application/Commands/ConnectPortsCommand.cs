namespace OneClick.Application.Commands;

/// <summary>
/// Contract for connecting two ports. A plan crossing is not a connection; connection validation is enforced by the application/core boundary.
/// </summary>
public sealed record ConnectPortsCommand(
    string PortAId,
    string PortBId,
    string? ConnectionNotes) : ProjectCommand
{
    [System.Text.Json.Serialization.JsonIgnore]
    public override string CommandType => CommandTypes.ConnectPorts;
}
