using System.Text.Json;
using OneClick.Core.Geometry;
using OneClick.Core.Ids;

namespace OneClick.Core.Domain;

/// <summary>
/// Loads the synthetic reference network fixture into minimal in-memory domain state.
/// Scope: synthetic baseline only. Does not infer undeclared physical parts or treat data as
/// manufacturer validation. Preserves documented baseline values from fixture data.
/// </summary>
public static class SyntheticReferenceNetworkLoader
{
    public const string FixtureSourceRef = "fixture:reference-network.json#synthetic-baseline";
    public const string DefaultMethodVersion = "synthetic-reference-network.v1";

    public static ProjectDomainState LoadBaselineFromFile(string fixturePath, ProjectId projectId)
    {
        Guard.Required(fixturePath, nameof(fixturePath));
        using var stream = File.OpenRead(fixturePath);
        return LoadBaseline(stream, projectId);
    }

    public static ProjectDomainState LoadBaseline(Stream jsonStream, ProjectId projectId)
    {
        ArgumentNullException.ThrowIfNull(jsonStream);
        if (string.IsNullOrWhiteSpace(projectId.Value))
            throw new ArgumentException("ProjectId is required.", nameof(projectId));

        using var doc = JsonDocument.Parse(jsonStream);
        return LoadBaseline(doc.RootElement, projectId);
    }

    public static ProjectDomainState LoadBaseline(JsonElement root, ProjectId projectId)
    {
        if (root.ValueKind != JsonValueKind.Object)
            throw new ArgumentException("Reference network fixture root must be a JSON object.", nameof(root));
        if (string.IsNullOrWhiteSpace(projectId.Value))
            throw new ArgumentException("ProjectId is required.", nameof(projectId));

        var units = root.TryGetProperty("units", out var unitsEl) ? unitsEl.GetString() : null;
        if (!string.Equals(units, "metres", StringComparison.Ordinal))
            throw new InvalidOperationException($"Synthetic reference network expects units 'metres', got '{units}'.");

        var state = new ProjectDomainState(projectId, revision: 0);
        var pieces = ReadPieces(root);
        state.ReplacePieces(pieces);
        return state;
    }

    public static IReadOnlyList<RoutePiece> ReadPieces(JsonElement root)
    {
        if (!root.TryGetProperty("pieces", out var piecesEl) || piecesEl.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException("Reference network fixture must contain a 'pieces' array.");

        var pieces = new List<RoutePiece>(piecesEl.GetArrayLength());
        var seenIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var p in piecesEl.EnumerateArray())
        {
            var piece = ReadPiece(p);
            if (!seenIds.Add(piece.Id))
                throw new InvalidOperationException($"Duplicate piece id '{piece.Id}' in synthetic reference network fixture.");
            pieces.Add(piece);
        }

        return pieces;
    }

    public static RoutePiece ReadPiece(JsonElement p)
    {
        var id = GetRequiredString(p, "id");
        var start = ReadPoint3D(p, "start_m");
        var end = ReadPoint3D(p, "end_m");
        var startOffset = GetRequiredDouble(p, "start_net_offset_m");
        var endOffset = GetRequiredDouble(p, "end_net_offset_m");
        var group = GetRequiredString(p, "group");

        var sourceRefs = new[] { FixtureSourceRef, $"fixture:reference-network.json#piece:{id}" };
        return new RoutePiece(id, start, end, startOffset, endOffset, group, sourceRefs, DefaultMethodVersion);
    }

    private static Point3D ReadPoint3D(JsonElement parent, string propertyName)
    {
        if (!parent.TryGetProperty(propertyName, out var array) || array.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException($"Property '{propertyName}' must be a 3-element array.");
        if (array.GetArrayLength() != 3)
            throw new InvalidOperationException($"Property '{propertyName}' must have exactly 3 elements.");

        return new Point3D(
            GetFinite(array[0], $"{propertyName}[0]"),
            GetFinite(array[1], $"{propertyName}[1]"),
            GetFinite(array[2], $"{propertyName}[2]"));
    }

    private static string GetRequiredString(JsonElement parent, string propertyName)
    {
        if (!parent.TryGetProperty(propertyName, out var el) || el.ValueKind != JsonValueKind.String)
            throw new InvalidOperationException($"Property '{propertyName}' must be a string.");
        var value = el.GetString();
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException($"Property '{propertyName}' must be non-empty.");
        return value;
    }

    private static double GetRequiredDouble(JsonElement parent, string propertyName)
    {
        if (!parent.TryGetProperty(propertyName, out var el))
            throw new InvalidOperationException($"Property '{propertyName}' is required.");
        return GetFinite(el, propertyName);
    }

    private static double GetFinite(JsonElement el, string name)
    {
        if (el.ValueKind != JsonValueKind.Number || !el.TryGetDouble(out var value) || !double.IsFinite(value))
            throw new InvalidOperationException($"Property '{name}' must be a finite number.");
        return value;
    }
}
