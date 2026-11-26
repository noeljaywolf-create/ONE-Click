namespace OneClick.Core.Provenance;

/// <summary>
/// Small calculation-result envelope carrying a derived value, required provenance, and
/// optional issues, warnings, and domain contributions. Compatible with
/// <see cref="IDerivedResult"/> and <see cref="DerivedResult{T}"/>.
/// </summary>
public sealed record CalculationResult<T> : IDerivedResult
{
    public CalculationResult(
        T value,
        ResultProvenance provenance,
        IReadOnlyList<string>? warnings = null,
        IReadOnlyList<string>? issues = null,
        IReadOnlyList<QuantityContribution>? contributions = null)
    {
        if (value is null)
            throw new ArgumentNullException(nameof(value));

        Value = value;
        Provenance = provenance ?? throw new ArgumentNullException(nameof(provenance));
        Warnings = NormalizeStrings(warnings, nameof(warnings));
        Issues = NormalizeStrings(issues, nameof(issues));
        Contributions = NormalizeContributions(contributions, nameof(contributions));
    }

    public T Value { get; }

    public ResultProvenance Provenance { get; }

    /// <summary>Non-fatal warnings associated with the calculation.</summary>
    public IReadOnlyList<string> Warnings { get; }

    /// <summary>Issues that affect interpretation, completeness, or review of the result.</summary>
    public IReadOnlyList<string> Issues { get; }

    /// <summary>Canonical domain contributions carried by or explaining the result.</summary>
    public IReadOnlyList<QuantityContribution> Contributions { get; }

    public DerivedResult<T> ToDerivedResult() => new(Value, Provenance);

    public static CalculationResult<T> FromDerivedResult(
        DerivedResult<T> result,
        IReadOnlyList<string>? warnings = null,
        IReadOnlyList<string>? issues = null,
        IReadOnlyList<QuantityContribution>? contributions = null)
    {
        ArgumentNullException.ThrowIfNull(result);
        return new CalculationResult<T>(result.Value, result.Provenance, warnings, issues, contributions);
    }

    private static IReadOnlyList<string> NormalizeStrings(IReadOnlyList<string>? values, string paramName)
    {
        if (values is null || values.Count == 0)
            return Array.Empty<string>();

        var copy = new string[values.Count];
        for (var i = 0; i < values.Count; i++)
            copy[i] = Guard.Required(values[i], $"{paramName}[{i}]");

        return Array.AsReadOnly(copy);
    }

    private static IReadOnlyList<QuantityContribution> NormalizeContributions(
        IReadOnlyList<QuantityContribution>? contributions,
        string paramName)
    {
        if (contributions is null || contributions.Count == 0)
            return Array.Empty<QuantityContribution>();

        var copy = new QuantityContribution[contributions.Count];
        for (var i = 0; i < contributions.Count; i++)
            copy[i] = contributions[i] ?? throw new ArgumentException("Contribution is required.", $"{paramName}[{i}]");

        return Array.AsReadOnly(copy);
    }
}
