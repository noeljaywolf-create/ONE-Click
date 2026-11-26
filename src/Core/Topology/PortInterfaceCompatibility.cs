namespace OneClick.Core.Topology;

/// <summary>
/// Result of a port interface compatibility evaluation. Used as a hook so Core validation can
/// reject incompatible interfaces/preparations without depending on catalogue storage.
/// </summary>
public sealed record PortInterfaceCompatibilityResult
{
    public PortInterfaceCompatibilityResult(bool isCompatible, IReadOnlyList<TopologyIssue>? issues = null)
    {
        IsCompatible = isCompatible;
        Issues = NormalizeIssues(issues);
        if (isCompatible && Issues.Count > 0)
            throw new ArgumentException("Compatible results must not carry incompatibility issues.", nameof(issues));
    }

    public bool IsCompatible { get; }

    public IReadOnlyList<TopologyIssue> Issues { get; }

    public static PortInterfaceCompatibilityResult Compatible() => new(true);

    public static PortInterfaceCompatibilityResult Incompatible(params TopologyIssue[] issues) =>
        new(false, issues);

    private static IReadOnlyList<TopologyIssue> NormalizeIssues(IReadOnlyList<TopologyIssue>? issues)
    {
        if (issues is null || issues.Count == 0)
            return Array.Empty<TopologyIssue>();

        var copy = new TopologyIssue[issues.Count];
        for (var i = 0; i < issues.Count; i++)
            copy[i] = issues[i] ?? throw new ArgumentNullException($"issues[{i}]");
        return Array.AsReadOnly(copy);
    }
}

/// <summary>
/// Catalogue/interface compatibility hook. Implementations may consult pinned catalogue data;
/// Core provides only the contract and a conservative default policy.
/// </summary>
public interface IPortInterfaceCompatibilityPolicy
{
    PortInterfaceCompatibilityResult Evaluate(Port portA, Port portB);
}

/// <summary>
/// Conservative default policy: interface type and preparation must match exactly when both are known.
/// Nominal size/interface equality is not treated as actual OD/ID equivalence; this hook only
/// evaluates explicit interface/preparation identifiers.
/// </summary>
public sealed class StrictPortInterfaceCompatibilityPolicy : IPortInterfaceCompatibilityPolicy
{
    public static StrictPortInterfaceCompatibilityPolicy Instance { get; } = new();

    public PortInterfaceCompatibilityResult Evaluate(Port portA, Port portB)
    {
        ArgumentNullException.ThrowIfNull(portA);
        ArgumentNullException.ThrowIfNull(portB);

        var issues = new List<TopologyIssue>();

        if (!string.Equals(portA.InterfaceSpec.InterfaceType, portB.InterfaceSpec.InterfaceType, StringComparison.Ordinal))
        {
            issues.Add(new TopologyIssue(
                TopologyIssueCodes.IncompatibleInterface,
                $"Interface types '{portA.InterfaceSpec.InterfaceType}' and '{portB.InterfaceSpec.InterfaceType}' are incompatible.",
                "interfaceType"));
        }

        if (!string.Equals(portA.InterfaceSpec.Preparation, portB.InterfaceSpec.Preparation, StringComparison.Ordinal))
        {
            issues.Add(new TopologyIssue(
                TopologyIssueCodes.IncompatiblePreparation,
                "Port preparations are incompatible for a confirmed connection.",
                "preparation"));
        }

        return issues.Count == 0
            ? PortInterfaceCompatibilityResult.Compatible()
            : new PortInterfaceCompatibilityResult(false, issues);
    }
}
