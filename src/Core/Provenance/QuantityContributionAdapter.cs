using AggregationQuantityContribution = OneClick.Core.Formulas.QuantityContribution;

namespace OneClick.Core.Provenance;

/// <summary>
/// Canonical adapter between minimal Q01 aggregation contributions
/// (<see cref="AggregationQuantityContribution"/>) and domain-aligned provenance contributions
/// (<see cref="QuantityContribution"/>).
/// </summary>
public static class QuantityContributionAdapter
{
    /// <summary>
    /// Maps a minimal aggregation contribution to the canonical domain-aligned contribution.
    /// Aggregation-only metadata (<c>IncludedByOwnerId</c>) is intentionally not mapped because
    /// it is not part of the domain contribution contract.
    /// </summary>
    public static QuantityContribution ToCanonical(
        AggregationQuantityContribution contribution,
        QuantityContributionId id,
        Ledger ledger,
        IReadOnlyList<string> sourceRefs,
        string methodVersion)
    {
        ArgumentNullException.ThrowIfNull(contribution);
        return contribution.ToProvenance(id, ledger, sourceRefs, methodVersion);
    }

    /// <summary>
    /// Maps aggregation contributions to canonical domain-aligned contributions. The id factory
    /// is called once per input contribution in input order; callers must provide stable,
    /// explicit ids when deterministic identity is required.
    /// </summary>
    public static IReadOnlyList<QuantityContribution> ToCanonical(
        IEnumerable<AggregationQuantityContribution> contributions,
        Ledger ledger,
        IReadOnlyList<string> sourceRefs,
        string methodVersion,
        Func<AggregationQuantityContribution, QuantityContributionId> idFactory)
    {
        ArgumentNullException.ThrowIfNull(contributions);
        ArgumentNullException.ThrowIfNull(idFactory);

        var mapped = new List<QuantityContribution>();
        foreach (var contribution in contributions)
        {
            ArgumentNullException.ThrowIfNull(contribution, nameof(contributions));
            var id = idFactory(contribution);
            mapped.Add(ToCanonical(contribution, id, ledger, sourceRefs, methodVersion));
        }

        return mapped.AsReadOnly();
    }

    /// <summary>
    /// Parses a Q01 string ledger identifier into the canonical <see cref="Ledger"/> enum.
    /// Accepts the canonical enum names case-insensitively.
    /// </summary>
    public static bool TryParseLedger(string ledger, out Ledger value)
    {
        if (string.IsNullOrWhiteSpace(ledger))
        {
            value = default;
            return false;
        }

        return Enum.TryParse(ledger.Trim(), ignoreCase: true, out value) && Enum.IsDefined(value);
    }

    /// <summary>
    /// Parses a Q01 string ledger identifier into the canonical <see cref="Ledger"/> enum.
    /// </summary>
    /// <exception cref="ArgumentException">When the ledger is not a known canonical ledger.</exception>
    public static Ledger ParseLedger(string ledger)
    {
        if (TryParseLedger(ledger, out var value))
            return value;

        throw new ArgumentException(
            $"Unknown canonical ledger '{ledger}'. Expected one of: {string.Join(", ", Enum.GetNames<Ledger>())}.",
            nameof(ledger));
    }
}
