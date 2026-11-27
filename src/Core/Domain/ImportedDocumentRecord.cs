namespace OneClick.Core.Domain;

/// <summary>
/// Minimal in-memory record of an imported document. Stores metadata/refs only; no PDF parsing.
/// </summary>
public sealed record ImportedDocumentRecord
{
    public ImportedDocumentRecord(
        string documentId,
        string uploadHandle,
        string? documentLabel,
        string? sourceFileName,
        IReadOnlyList<string> regionIds,
        IReadOnlyList<string> sourceRefs,
        string methodVersion)
    {
        DocumentId = Guard.Required(documentId, nameof(documentId));
        UploadHandle = Guard.Required(uploadHandle, nameof(uploadHandle));
        DocumentLabel = string.IsNullOrWhiteSpace(documentLabel) ? null : documentLabel;
        SourceFileName = string.IsNullOrWhiteSpace(sourceFileName) ? null : sourceFileName;
        RegionIds = NormalizeIds(regionIds, nameof(regionIds));
        SourceRefs = NormalizeIds(sourceRefs, nameof(sourceRefs));
        MethodVersion = Guard.Required(methodVersion, nameof(methodVersion));
    }

    public string DocumentId { get; }
    public string UploadHandle { get; }
    public string? DocumentLabel { get; }
    public string? SourceFileName { get; }
    public IReadOnlyList<string> RegionIds { get; }
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
