using OneClick.Application.Contracts;
using OneClick.Core.Ids;

namespace OneClick.Application.Agents.Audit;

using OneClick.Application.Agents;

/// <summary>
/// Immutable audit/provenance record for a local agent execution.
/// Captures identity, correlation, timing, outcome, and inspectable references without
/// claiming external AI/provider behavior.
/// </summary>
public sealed record AgentRunRecord(
    AgentRunId RunId,
    AgentId AgentId,
    string AgentVersion,
    AgentKind AgentKind,
    string CorrelationId,
    ProjectId ProjectId,
    ProjectRevision ProjectRevision,
    ActorContext Actor,
    string? InputFingerprint,
    string? InputSummary,
    DateTimeOffset StartedAt,
    DateTimeOffset? EndedAt,
    AgentRunStatus Status,
    IReadOnlyList<string> Issues,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<string> ProvenanceRefs,
    IReadOnlyList<string> OutputRefs,
    string? OutputSummary,
    string? ErrorCode = null,
    string? ErrorDetail = null)
{
    public AgentRunId RunId { get; } = RequireRunId(RunId);
    public AgentId AgentId { get; } = RequireAgentId(AgentId);
    public string AgentVersion { get; } = Require(AgentVersion, nameof(AgentVersion));
    public string CorrelationId { get; } = Require(CorrelationId, nameof(CorrelationId));
    public ActorContext Actor { get; } = Actor ?? throw new ArgumentNullException(nameof(Actor));
    public IReadOnlyList<string> Issues { get; } = Normalize(Issues, nameof(Issues));
    public IReadOnlyList<string> Warnings { get; } = Normalize(Warnings, nameof(Warnings));
    public IReadOnlyList<string> ProvenanceRefs { get; } = Normalize(ProvenanceRefs, nameof(ProvenanceRefs));
    public IReadOnlyList<string> OutputRefs { get; } = Normalize(OutputRefs, nameof(OutputRefs));

    private static AgentRunId RequireRunId(AgentRunId runId)
    {
        if (string.IsNullOrWhiteSpace(runId.Value))
            throw new ArgumentException("Run id is required.", nameof(runId));

        return runId;
    }

    private static AgentId RequireAgentId(AgentId agentId)
    {
        if (string.IsNullOrWhiteSpace(agentId.Value))
            throw new ArgumentException("Agent id is required.", nameof(agentId));

        return agentId;
    }

    private static string Require(string value, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Value is required.", paramName);

        return value;
    }

    private static IReadOnlyList<string> Normalize(IReadOnlyList<string>? values, string paramName)
    {
        if (values is null || values.Count == 0)
            return Array.Empty<string>();

        var copy = new string[values.Count];
        for (var i = 0; i < values.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(values[i]))
                throw new ArgumentException("Value is required.", $"{paramName}[{i}]");

            copy[i] = values[i];
        }

        return Array.AsReadOnly(copy);
    }
}
