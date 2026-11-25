namespace OneClick.Application.Commands;

/// <summary>
/// Contract for placing a component. Component ports come from pinned catalogue definitions;
/// this command does not carry direct domain-table writes.
/// </summary>
public sealed record PlaceComponentCommand(
    string? ComponentId,
    string CataloguePartId,
    string CatalogueReleaseId,
    ComponentPlacement Placement,
    IReadOnlyDictionary<string, string>? Properties = null) : ProjectCommand
{
    [System.Text.Json.Serialization.JsonIgnore]
    public override string CommandType => CommandTypes.PlaceComponent;
}

/// <summary>
/// Placement contract in model coordinates. Detailed convention validation is performed at the application/core boundary.
/// </summary>
public sealed record ComponentPlacement(
    double X,
    double Y,
    double? Z,
    double? RotationRadians);
