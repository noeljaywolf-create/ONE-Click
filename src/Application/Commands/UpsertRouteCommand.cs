namespace OneClick.Application.Commands;

/// <summary>
/// Contract for creating or updating a route. The application boundary must atomically validate and create/update
/// derived route-end port identities; referenced existing ports cannot be silently orphaned.
/// </summary>
public sealed record UpsertRouteCommand(
    string? RouteId,
    IReadOnlyList<RouteVertex> Vertices,
    string? StartPortId,
    string? EndPortId,
    string? RouteNotes) : ProjectCommand
{
    [System.Text.Json.Serialization.JsonIgnore]
    public override string CommandType => CommandTypes.UpsertRoute;
}

/// <summary>
/// Route vertex contract. Z is a qualified metre value when present; absent elevation is represented as null.
/// </summary>
public sealed record RouteVertex(
    double X,
    double Y,
    double? Z);
