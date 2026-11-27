using OneClick.Application.Agents;

namespace OneClick.Desktop.Services;

/// <summary>
/// UI-facing metadata for a local agent execution. Captures deterministic run facts only;
/// it does not fabricate engineering outcomes.
/// </summary>
public sealed record AgentRunRecord(
    string CorrelationId,
    string AgentId,
    string AgentName,
    DateTimeOffset StartedAt,
    DateTimeOffset FinishedAt,
    bool IsSuccess,
    string? ErrorCode,
    int IssueCount,
    int WarningCount,
    int ProvenanceRefCount)
{
    public TimeSpan Duration => FinishedAt - StartedAt;

    public static AgentRunRecord FromResult(
        string correlationId,
        AgentDescriptor descriptor,
        DateTimeOffset startedAt,
        DateTimeOffset finishedAt,
        bool isSuccess,
        string? errorCode,
        int issueCount,
        int warningCount,
        int provenanceRefCount)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);
        ArgumentNullException.ThrowIfNull(descriptor);

        return new AgentRunRecord(
            correlationId,
            descriptor.Id.Value,
            descriptor.Name,
            startedAt,
            finishedAt,
            isSuccess,
            errorCode,
            issueCount,
            warningCount,
            provenanceRefCount);
    }
}
