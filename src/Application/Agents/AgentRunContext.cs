using OneClick.Application.Contracts;
using OneClick.Core.Ids;

namespace OneClick.Application.Agents;

/// <summary>
/// Execution context for a local agent run. Carries project/actor identity and correlation
/// metadata; it does not grant authorization.
/// </summary>
public sealed record AgentRunContext(
    ProjectId ProjectId,
    ActorContext Actor,
    string CorrelationId,
    CancellationToken CancellationToken)
{
    public string CorrelationId { get; } = Require(CorrelationId, nameof(CorrelationId));

    private static string Require(string value, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Value is required.", paramName);

        return value;
    }
}
