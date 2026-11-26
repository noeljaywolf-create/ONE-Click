namespace OneClick.Core.Domain;

/// <summary>
/// Immutable frozen snapshot manifest for the first build slice.
/// Carries input revision, method/source versions, known/provisional/unknown quantities,
/// and exclusions/issues. Unknown quantities are preserved as unknown (value null), never zero.
/// </summary>
public sealed record SnapshotManifest
{
    public SnapshotManifest(
        string snapshotId,
        string projectId,
        long projectRevision,
        DateTimeOffset createdAtUtc,
        string? label = null,
        IReadOnlyList<string>? sourceRefs = null,
        string? methodVersion = null,
        IReadOnlyList<SnapshotQuantityLine>? knownQuantities = null,
        IReadOnlyList<SnapshotQuantityLine>? provisionalQuantities = null,
        IReadOnlyList<SnapshotQuantityLine>? unknownQuantities = null,
        IReadOnlyList<string>? exclusions = null,
        IReadOnlyList<string>? issues = null)
    {
        SnapshotId = Guard.Required(snapshotId, nameof(snapshotId));
        ProjectId = Guard.Required(projectId, nameof(projectId));
        if (projectRevision < 0) throw new ArgumentOutOfRangeException(nameof(projectRevision), projectRevision, "Revision must be >= 0.");
        ProjectRevision = projectRevision;
        CreatedAtUtc = createdAtUtc;
        Label = label;
        SourceRefs = NormalizeStrings(sourceRefs);
        MethodVersion = methodVersion;
        KnownQuantities = NormalizeQuantities(knownQuantities);
        ProvisionalQuantities = NormalizeQuantities(provisionalQuantities);
        UnknownQuantities = NormalizeQuantities(unknownQuantities);
        Exclusions = NormalizeStrings(exclusions);
        Issues = NormalizeStrings(issues);
    }

    public string SnapshotId { get; }
    public string ProjectId { get; }

    /// <summary>Frozen project/input revision at snapshot time.</summary>
    public long ProjectRevision { get; }

    public DateTimeOffset CreatedAtUtc { get; }
    public string? Label { get; }
    public IReadOnlyList<string> SourceRefs { get; }
    public string? MethodVersion { get; }

    /// <summary>Known quantity totals/lines. Separated from provisional and unknown.</summary>
    public IReadOnlyList<SnapshotQuantityLine> KnownQuantities { get; }

    /// <summary>Provisional quantity totals/lines. Separated from known and unknown.</summary>
    public IReadOnlyList<SnapshotQuantityLine> ProvisionalQuantities { get; }

    /// <summary>Unknown quantity totals/lines. Value is null; never coerced to zero.</summary>
    public IReadOnlyList<SnapshotQuantityLine> UnknownQuantities { get; }

    /// <summary>Explicit exclusions applied to this frozen snapshot.</summary>
    public IReadOnlyList<string> Exclusions { get; }

    /// <summary>Issues affecting interpretation/completeness of this snapshot.</summary>
    public IReadOnlyList<string> Issues { get; }

    private static IReadOnlyList<string> NormalizeStrings(IReadOnlyList<string>? values)
    {
        if (values is null || values.Count == 0) return Array.Empty<string>();
        var copy = new string[values.Count];
        for (var i = 0; i < values.Count; i++)
            copy[i] = Guard.Required(values[i], $"values[{i}]");
        return Array.AsReadOnly(copy);
    }

    private static IReadOnlyList<SnapshotQuantityLine> NormalizeQuantities(IReadOnlyList<SnapshotQuantityLine>? values)
    {
        if (values is null || values.Count == 0) return Array.Empty<SnapshotQuantityLine>();
        var copy = new SnapshotQuantityLine[values.Count];
        for (var i = 0; i < values.Count; i++)
            copy[i] = values[i] ?? throw new ArgumentException("Quantity line is required.", $"values[{i}]");
        return Array.AsReadOnly(copy);
    }
}

/// <summary>
/// Immutable quantity line in a frozen snapshot export/manifest.
/// Known/provisional lines carry a numeric value; unknown lines carry null value and reason/issues.
/// </summary>
public sealed record SnapshotQuantityLine
{
    public SnapshotQuantityLine(
        string ledger,
        string groupKey,
        string unit,
        double? value,
        string status,
        string? reason = null,
        IReadOnlyList<string>? sourceRefs = null,
        string? methodVersion = null,
        IReadOnlyList<string>? issueRefs = null)
    {
        Ledger = Guard.Required(ledger, nameof(ledger));
        GroupKey = Guard.Required(groupKey, nameof(groupKey));
        Unit = Guard.Required(unit, nameof(unit));
        Status = Guard.Required(status, nameof(status));
        if (string.Equals(Status, SnapshotQuantityStatus.Unknown, StringComparison.Ordinal))
        {
            if (value is not null)
                throw new ArgumentException("Unknown quantity line must not have a numeric value; use null.", nameof(value));
            Reason = Guard.Required(reason, nameof(reason));
        }
        else
        {
            if (value is null || !double.IsFinite(value.Value))
                throw new ArgumentException("Known/provisional quantity line requires a finite numeric value.", nameof(value));
        }

        Value = value;
        Reason = reason;
        SourceRefs = NormalizeStrings(sourceRefs);
        MethodVersion = methodVersion;
        IssueRefs = NormalizeStrings(issueRefs);
    }

    public string Ledger { get; }
    public string GroupKey { get; }
    public string Unit { get; }

    /// <summary>Numeric value for known/provisional; null for unknown. Null never means zero.</summary>
    public double? Value { get; }

    /// <summary>Known, Provisional, or Unknown.</summary>
    public string Status { get; }

    public string? Reason { get; }
    public IReadOnlyList<string> SourceRefs { get; }
    public string? MethodVersion { get; }
    public IReadOnlyList<string> IssueRefs { get; }

    private static IReadOnlyList<string> NormalizeStrings(IReadOnlyList<string>? values)
    {
        if (values is null || values.Count == 0) return Array.Empty<string>();
        var copy = new string[values.Count];
        for (var i = 0; i < values.Count; i++)
            copy[i] = Guard.Required(values[i], $"values[{i}]");
        return Array.AsReadOnly(copy);
    }
}

public static class SnapshotQuantityStatus
{
    public const string Known = "Known";
    public const string Provisional = "Provisional";
    public const string Unknown = "Unknown";
}
