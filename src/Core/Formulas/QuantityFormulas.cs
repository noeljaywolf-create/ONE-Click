using OneClick.Core.TaggedValues;
using OneClick.Core.Units;

namespace OneClick.Core.Formulas;

/// <summary>
/// Minimal quantity contribution for Q01 aggregation. Ownership is explicit so assembly
/// contents can be deduplicated without silently excluding included items.
/// </summary>
public sealed record QuantityContribution
{
    public QuantityContribution(
        string ownerId,
        string ledger,
        string specGroup,
        double value,
        SiUnit unit,
        string? includedByOwnerId = null)
    {
        OwnerId = Guard.Required(ownerId, nameof(ownerId));
        Ledger = Guard.Required(ledger, nameof(Ledger));
        SpecGroup = Guard.Required(specGroup, nameof(SpecGroup));
        Value = Guard.Finite(value, nameof(value));
        if (!Enum.IsDefined(unit))
            throw new ArgumentOutOfRangeException(nameof(unit), unit, "Unit must be a defined SI unit.");
        Unit = unit;
        IncludedByOwnerId = string.IsNullOrWhiteSpace(includedByOwnerId) ? null : includedByOwnerId;
    }

    public string OwnerId { get; }
    public string Ledger { get; }
    public string SpecGroup { get; }
    public double Value { get; }
    public SiUnit Unit { get; }

    /// <summary>
    /// When set, this contribution is included by the named assembly owner and must not be
    /// double-counted when that assembly owner is counted in the same ledger/spec/group.
    /// </summary>
    public string? IncludedByOwnerId { get; }
}

public sealed record QuantityAggregateResult(
    KnownValue Total,
    IReadOnlyList<string> CountedOwnerIds,
    IReadOnlyList<string> ExcludedOwnerIds,
    IReadOnlyList<string> Warnings)
{
    public KnownValue Total { get; } = Total ?? throw new ArgumentNullException(nameof(Total));
    public IReadOnlyList<string> CountedOwnerIds { get; } = CountedOwnerIds ?? throw new ArgumentNullException(nameof(CountedOwnerIds));
    public IReadOnlyList<string> ExcludedOwnerIds { get; } = ExcludedOwnerIds ?? throw new ArgumentNullException(nameof(ExcludedOwnerIds));
    public IReadOnlyList<string> Warnings { get; } = Warnings ?? throw new ArgumentNullException(nameof(Warnings));
}

public static class QuantityFormulas
{
    public static FormulaDescriptor Q01 { get; } = new(
        Id: "Q01",
        MethodVersion: "Q01.1.0",
        OutputUnit: SiUnit.Meter, // Default/documented for length; actual result unit is validated from contributions.
        Formula: "sum(unique owned contributions by ledger/spec/group)",
        Applicability: "Sum unique owned contributions by ledger/spec/group; deduplicate assembly ownership.",
        FailureHandling: "Rejects mixed units, non-finite values, or ambiguous duplicate ownership with conflicting values; does not silently exclude included items.");

    /// <summary>
    /// Q01 basic: sum unique owned contributions by ledger/spec/group.
    /// Deduplicates by owner within the requested group. Contributions included by a counted
    /// assembly owner are excluded from direct sum to prevent double counting, and this exclusion
    /// is reported explicitly.
    /// </summary>
    public static QuantityAggregateResult SumUniqueOwnedContributions(
        IEnumerable<QuantityContribution> contributions,
        string ledger,
        string specGroup,
        SiUnit unit,
        IReadOnlyList<string> sourceRefs)
    {
        ArgumentNullException.ThrowIfNull(contributions);
        Guard.Required(ledger, nameof(ledger));
        Guard.Required(specGroup, nameof(specGroup));
        if (!Enum.IsDefined(unit))
            throw new ArgumentOutOfRangeException(nameof(unit), unit, "Unit must be a defined SI unit.");
        ArgumentNullException.ThrowIfNull(sourceRefs);

        var inGroup = contributions
            .Where(c => string.Equals(c.Ledger, ledger, StringComparison.Ordinal))
            .Where(c => string.Equals(c.SpecGroup, specGroup, StringComparison.Ordinal))
            .ToList();

        foreach (var c in inGroup)
        {
            if (c.Unit != unit)
            {
                throw new InvalidOperationException(
                    $"Q01 mixed units in ledger '{ledger}', spec/group '{specGroup}': owner '{c.OwnerId}' has {c.Unit}, expected {unit}.");
            }
        }

        // Deduplicate by owner id. Same owner with same value is idempotent; conflicting value is ambiguous.
        var byOwner = new Dictionary<string, QuantityContribution>(StringComparer.Ordinal);
        foreach (var c in inGroup)
        {
            if (byOwner.TryGetValue(c.OwnerId, out var existing))
            {
                if (existing.Value != c.Value || existing.IncludedByOwnerId != c.IncludedByOwnerId)
                {
                    throw new InvalidOperationException(
                        $"Q01 ambiguous duplicate ownership for owner '{c.OwnerId}' in ledger '{ledger}', spec/group '{specGroup}'.");
                }

                continue; // identical duplicate contribution
            }

            byOwner.Add(c.OwnerId, c);
        }

        var owners = byOwner.Keys.ToHashSet(StringComparer.Ordinal);
        var counted = new List<string>();
        var excluded = new List<string>();
        var warnings = new List<string>();
        double total = 0d;

        foreach (var c in byOwner.Values.OrderBy(c => c.OwnerId, StringComparer.Ordinal))
        {
            if (c.IncludedByOwnerId is not null && owners.Contains(c.IncludedByOwnerId))
            {
                excluded.Add(c.OwnerId);
                warnings.Add($"excluded:owner:{c.OwnerId}:included-by:{c.IncludedByOwnerId}");
                continue;
            }

            counted.Add(c.OwnerId);
            total = Guard.Finite(total + c.Value, nameof(total));
        }

        var totalValue = new KnownValue(total, unit, sourceRefs, Q01.MethodVersion);
        return new QuantityAggregateResult(totalValue, counted, excluded, warnings);
    }
}
