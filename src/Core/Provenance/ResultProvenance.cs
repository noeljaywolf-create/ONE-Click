namespace OneClick.Core.Provenance;

/// <summary>
/// Immutable provenance metadata for a derived result. Every derived result must carry
/// input versions, method version, and traceable contributions.
/// </summary>
public sealed record ResultProvenance
{
    public ResultProvenance(
        string methodVersion,
        IReadOnlyList<InputReference> inputs,
        IReadOnlyList<string> sourceRefs,
        IReadOnlyList<QuantityContributionId> contributionIds)
    {
        MethodVersion = Guard.Required(methodVersion, nameof(methodVersion));
        Inputs = NormalizeInputs(inputs, nameof(inputs));
        SourceRefs = NormalizeRefs(sourceRefs, nameof(sourceRefs));
        ContributionIds = NormalizeContributionIds(contributionIds, nameof(contributionIds));
    }

    /// <summary>Version of the method/calculation that produced the result.</summary>
    public string MethodVersion { get; }

    /// <summary>Versioned input references/fingerprints used to derive the result.</summary>
    public IReadOnlyList<InputReference> Inputs { get; }

    /// <summary>Source references captured for traceability.</summary>
    public IReadOnlyList<string> SourceRefs { get; }

    /// <summary>Traceable contribution identities that compose or explain the result.</summary>
    public IReadOnlyList<QuantityContributionId> ContributionIds { get; }

    private static IReadOnlyList<InputReference> NormalizeInputs(IReadOnlyList<InputReference>? inputs, string paramName)
    {
        ArgumentNullException.ThrowIfNull(inputs, paramName);
        if (inputs.Count == 0)
            return Array.Empty<InputReference>();

        var copy = new InputReference[inputs.Count];
        for (var i = 0; i < inputs.Count; i++)
            copy[i] = inputs[i] ?? throw new ArgumentException("Input reference is required.", $"{paramName}[{i}]");
        return Array.AsReadOnly(copy);
    }

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

    private static IReadOnlyList<QuantityContributionId> NormalizeContributionIds(
        IReadOnlyList<QuantityContributionId>? ids,
        string paramName)
    {
        ArgumentNullException.ThrowIfNull(ids, paramName);
        if (ids.Count == 0)
            return Array.Empty<QuantityContributionId>();

        var copy = new QuantityContributionId[ids.Count];
        for (var i = 0; i < ids.Count; i++)
        {
            var id = ids[i];
            if (string.IsNullOrWhiteSpace(id.Value))
                throw new ArgumentException("Contribution id is required.", $"{paramName}[{i}]");
            copy[i] = id;
        }

        return Array.AsReadOnly(copy);
    }
}
