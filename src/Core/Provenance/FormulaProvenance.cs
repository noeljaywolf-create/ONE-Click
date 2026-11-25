namespace OneClick.Core.Provenance;

using OneClick.Core.TaggedValues;

/// <summary>
/// Adapters for wiring formula/calculation outputs to <see cref="ResultProvenance"/>.
/// Keeps Core provenance explicit without changing existing formula return contracts.
/// </summary>
public static class FormulaProvenance
{
    public static ResultProvenance Create(
        string methodVersion,
        IReadOnlyList<InputReference> inputs,
        IReadOnlyList<string> sourceRefs,
        IReadOnlyList<QuantityContributionId> contributionIds) =>
        new(methodVersion, inputs, sourceRefs, contributionIds);

    /// <summary>
    /// Wraps a derived value with explicit provenance. Use when source refs/method version are
    /// not carried by the value itself.
    /// </summary>
    public static DerivedResult<T> Wrap<T>(
        T value,
        string methodVersion,
        IReadOnlyList<InputReference> inputs,
        IReadOnlyList<string> sourceRefs,
        IReadOnlyList<QuantityContributionId> contributionIds)
        where T : notnull =>
        new(value, Create(methodVersion, inputs, sourceRefs, contributionIds));

    /// <summary>
    /// Wraps a tagged formula output. Method version and source refs are taken from the tagged
    /// value to keep the provenance contract consistent with the calculation output.
    /// </summary>
    public static DerivedResult<TTagged> WrapTagged<TTagged>(
        TTagged value,
        IReadOnlyList<InputReference> inputs,
        IReadOnlyList<QuantityContributionId> contributionIds)
        where TTagged : TaggedValue =>
        new(value, Create(value.MethodVersion, inputs, value.SourceRefs, contributionIds));

    /// <summary>
    /// Wraps a tagged formula output when the caller wants to supply explicit source refs.
    /// </summary>
    public static DerivedResult<TTagged> WrapTagged<TTagged>(
        TTagged value,
        IReadOnlyList<InputReference> inputs,
        IReadOnlyList<string> sourceRefs,
        IReadOnlyList<QuantityContributionId> contributionIds)
        where TTagged : TaggedValue =>
        new(value, Create(value.MethodVersion, inputs, sourceRefs, contributionIds));
}
