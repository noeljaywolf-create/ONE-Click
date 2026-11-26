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
    {
        Guard.Required(portA.Value, nameof(portA));
        Guard.Required(portB.Value, nameof(portB));
        if (string.Equals(portA.Value, portB.Value, StringComparison.Ordinal))
            throw new ArgumentException("A candidate result requires two distinct ports.", nameof(portB));

        PortA = portA;
        PortB = portB;
        Outcome = ValidateOutcome(outcome, nameof(outcome));
        IssueIds = NormalizeRefs(issueIds);
    }

    public PortId PortA { get; }

    public PortId PortB { get; }

    public TopologyValidationOutcome Outcome { get; }

    public IReadOnlyList<string> IssueIds { get; }

    private static TopologyValidationOutcome ValidateOutcome(TopologyValidationOutcome outcome, string paramName)
    {
        if (!Enum.IsDefined(outcome))
            throw new ArgumentOutOfRangeException(paramName, outcome, "Topology validation outcome must be defined.");
        return outcome;
    }

    private static IReadOnlyList<string> NormalizeRefs(IReadOnlyList<string>? refs)
    {
        if (refs is null || refs.Count == 0)
            return Array.Empty<string>();

        var copy = new string[refs.Count];
        for (var i = 0; i < refs.Count; i++)
            copy[i] = Guard.Required(refs[i], $"issueIds[{i}]");
        return Array.AsReadOnly(copy);
    }
}
