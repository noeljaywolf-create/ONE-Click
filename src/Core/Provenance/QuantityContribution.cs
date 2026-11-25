using OneClick.Core.TaggedValues;

namespace OneClick.Core.Provenance;

/// <summary>
/// Domain-aligned quantity contribution identity and provenance basics. Distinct from the
/// minimal Q01 aggregation record in <c>OneClick.Core.Formulas</c>.
/// </summary>
public sealed record QuantityContribution
{
    public QuantityContribution(
        QuantityContributionId id,
        string ownerId,
        Ledger ledger,
        string groupKey,
        TaggedValue qualifiedValue,
        IReadOnlyList<string> sourceRefs,
        string methodVersion)
    {
        if (string.IsNullOrWhiteSpace(id.Value))
            throw new ArgumentException("Contribution id is required.", nameof(id));
        Id = id;
        OwnerId = Guard.Required(ownerId, nameof(ownerId));
        if (!Enum.IsDefined(ledger))
            throw new ArgumentOutOfRangeException(nameof(ledger), ledger, "Ledger must be defined.");
        Ledger = ledger;
        GroupKey = Guard.Required(groupKey, nameof(groupKey));
        QualifiedValue = qualifiedValue ?? throw new ArgumentNullException(nameof(qualifiedValue));
        SourceRefs = NormalizeRefs(sourceRefs, nameof(sourceRefs));
        MethodVersion = Guard.Required(methodVersion, nameof(methodVersion));
    }

    public QuantityContributionId Id { get; }
    public string OwnerId { get; }
    public Ledger Ledger { get; }
    public string GroupKey { get; }
    public TaggedValue QualifiedValue { get; }
    public IReadOnlyList<string> SourceRefs { get; }
    public string MethodVersion { get; }

    private static IReadOnlyList<string> NormalizeRefs(IReadOnlyList<string>? refs, string paramName)
    {
        ArgumentNullException.ThrowIfNull(refs, paramName);
        if (refs.Count == 0)
            return Array.Empty<string>();

        var copy = new string[refs.Count];
        for (var i = 0; i < refs.Count; i++)
            copy[i] = Guard.Required(refs[i], $"{paramName}[{i}]");
        return Array.AsReadOnly(copy);
    }
}
