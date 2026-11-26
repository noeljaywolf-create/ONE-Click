using OneClick.Core.Ids;

namespace OneClick.Core.Topology;

/// <summary>
/// Lightweight topology validation result shape for a connection candidate. This models explicit
/// outcomes only; it does not implement a geometry engine or topology solver.
/// </summary>
public sealed record CandidateResult
{
    public CandidateResult(
        PortId portA,
        PortId portB,
        TopologyValidationOutcome outcome,
        IReadOnlyList<string>? issueIds = null)
        : this(portA, portB, outcome, NormalizeLegacyIssueIds(issueIds), Array.Empty<TopologyIssue>())
    {
    }

    public CandidateResult(
        PortId portA,
        PortId portB,
        TopologyValidationOutcome outcome,
        IReadOnlyList<TopologyIssue> issues)
        : this(portA, portB, outcome, DeriveIssueIds(issues), NormalizeIssues(issues))
    {
    }

    private CandidateResult(
        PortId portA,
        PortId portB,
        TopologyValidationOutcome outcome,
        IReadOnlyList<string> issueIds,
        IReadOnlyList<TopologyIssue> issues)
    {
        Guard.Required(portA.Value, nameof(portA));
        Guard.Required(portB.Value, nameof(portB));
        if (string.Equals(portA.Value, portB.Value, StringComparison.Ordinal))
            throw new ArgumentException("A candidate result requires two distinct ports.", nameof(portB));

        PortA = portA;
        PortB = portB;
        Outcome = ValidateOutcome(outcome, nameof(outcome));
        IssueIds = issueIds;
        Issues = issues;
    }

    public PortId PortA { get; }

    public PortId PortB { get; }

    public TopologyValidationOutcome Outcome { get; }

    /// <summary>Legacy string issue IDs/codes. Derived from <see cref="Issues"/> when typed issues are provided.</summary>
    public IReadOnlyList<string> IssueIds { get; }

    /// <summary>Typed validation issues. Empty when the result was constructed with legacy string issue IDs only.</summary>
    public IReadOnlyList<TopologyIssue> Issues { get; }

    private static TopologyValidationOutcome ValidateOutcome(TopologyValidationOutcome outcome, string paramName)
    {
        if (!Enum.IsDefined(outcome))
            throw new ArgumentOutOfRangeException(paramName, outcome, "Topology validation outcome must be defined.");
        return outcome;
    }

    private static IReadOnlyList<string> NormalizeLegacyIssueIds(IReadOnlyList<string>? refs)
    {
        if (refs is null || refs.Count == 0)
            return Array.Empty<string>();

        var copy = new string[refs.Count];
        for (var i = 0; i < refs.Count; i++)
            copy[i] = Guard.Required(refs[i], $"issueIds[{i}]");
        return Array.AsReadOnly(copy);
    }

    private static IReadOnlyList<TopologyIssue> NormalizeIssues(IReadOnlyList<TopologyIssue>? issues)
    {
        if (issues is null || issues.Count == 0)
            return Array.Empty<TopologyIssue>();

        var copy = new TopologyIssue[issues.Count];
        for (var i = 0; i < issues.Count; i++)
            copy[i] = issues[i] ?? throw new ArgumentNullException($"issues[{i}]");
        return Array.AsReadOnly(copy);
    }

    private static IReadOnlyList<string> DeriveIssueIds(IReadOnlyList<TopologyIssue>? issues)
    {
        if (issues is null || issues.Count == 0)
            return Array.Empty<string>();

        var copy = new string[issues.Count];
        for (var i = 0; i < issues.Count; i++)
        {
            var issue = issues[i] ?? throw new ArgumentNullException($"issues[{i}]");
            copy[i] = issue.Code;
        }

        return Array.AsReadOnly(copy);
    }
}
