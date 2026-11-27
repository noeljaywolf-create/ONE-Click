namespace OneClick.Core.Domain;

/// <summary>
/// Minimal in-memory document region identity. Import processing must create region identities before calibration.
/// Stores metadata/refs only.
/// </summary>
public sealed record DocumentRegionRecord
{
    public DocumentRegionRecord(
        string regionId,
        string documentId,
        IReadOnlyList<string> sourceRefs,
        string methodVersion)
    {
        RegionId = Guard.Required(regionId, nameof(regionId));
        DocumentId = Guard.Required(documentId, nameof(documentId));
        SourceRefs = NormalizeIds(sourceRefs, nameof(sourceRefs));
        MethodVersion = Guard.Required(methodVersion, nameof(methodVersion));
    }

    public string RegionId { get; }
    public string DocumentId { get; }
    public IReadOnlyList<string> SourceRefs { get; }
    public string MethodVersion { get; }

    private static IReadOnlyList<string> NormalizeIds(IReadOnlyList<string>? values, string paramName)
    {
        ArgumentNullException.ThrowIfNull(values, paramName);
        if (values.Count == 0) return Array.Empty<string>();
        var copy = new string[values.Count];
        for (var i = 0; i < values.Count; i++)
            copy[i] = Guard.Required(values[i], $"{paramName}[{i}]");
        return Array.AsReadOnly(copy);
    }
}
