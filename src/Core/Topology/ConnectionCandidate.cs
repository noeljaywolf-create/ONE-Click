using OneClick.Core.Ids;

namespace OneClick.Core.Topology;

/// <summary>
/// Lightweight connection candidate. A crossing is not a connection and a nearby endpoint is only
/// a candidate until validated. Confirmation must be represented explicitly.
/// </summary>
public sealed record ConnectionCandidate
{
    public ConnectionCandidate(
        ConnectionCandidateId id,
        PortId portA,
        PortId portB,
        ConnectionCandidateStatus status = ConnectionCandidateStatus.Candidate,
        TopologyValidationOutcome? validationOutcome = null,
        string? reason = null)
    {
        Guard.Required(id.Value, nameof(id));
        Id = id;

        Guard.Required(portA.Value, nameof(portA));
        Guard.Required(portB.Value, nameof(portB));
        if (string.Equals(portA.Value, portB.Value, StringComparison.Ordinal))
            throw new ArgumentException("A connection candidate requires two distinct ports.", nameof(portB));

        PortA = portA;
        PortB = portB;
        Status = ValidateStatus(status, nameof(status));
        ValidationOutcome = validationOutcome is null ? null : ValidateOutcome(validationOutcome.Value, nameof(validationOutcome));
        Reason = reason is null ? null : Guard.Required(reason, nameof(reason));
    }

    public ConnectionCandidateId Id { get; }

    public PortId PortA { get; }

    public PortId PortB { get; }

    public ConnectionCandidateStatus Status { get; }

    public TopologyValidationOutcome? ValidationOutcome { get; }

    public string? Reason { get; }

    private static ConnectionCandidateStatus ValidateStatus(ConnectionCandidateStatus status, string paramName)
    {
        if (!Enum.IsDefined(status))
            throw new ArgumentOutOfRangeException(paramName, status, "Connection candidate status must be defined.");
        return status;
    }

    private static TopologyValidationOutcome ValidateOutcome(TopologyValidationOutcome outcome, string paramName)
    {
        if (!Enum.IsDefined(outcome))
            throw new ArgumentOutOfRangeException(paramName, outcome, "Topology validation outcome must be defined.");
        return outcome;
    }
}
