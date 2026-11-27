using OneClick.Core.Ids;

namespace OneClick.Application.Agents.Audit;

/// <summary>
/// Append-only audit/provenance store for local agent runs.
/// Implementations must preserve recorded fields; no cloud/AI dependency.
/// </summary>
public interface IAgentRunRecordStore
{
    Task AppendAsync(AgentRunRecord record, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AgentRunRecord>> GetByProjectAsync(ProjectId projectId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AgentRunRecord>> GetByCorrelationIdAsync(string correlationId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AgentRunRecord>> GetRecentAsync(int maxCount, CancellationToken cancellationToken = default);
}
