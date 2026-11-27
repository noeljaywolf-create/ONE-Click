using OneClick.Core.Ids;

namespace OneClick.Application.Agents.Audit;

/// <summary>
/// Process-local in-memory append-only store for agent run audit records. Not durable.
/// </summary>
public sealed class InMemoryAgentRunRecordStore : IAgentRunRecordStore
{
    private readonly object _gate = new();
    private readonly List<AgentRunRecord> _records = new();

    public Task AppendAsync(AgentRunRecord record, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(record);

        lock (_gate)
        {
            _records.Add(record);
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<AgentRunRecord>> GetByProjectAsync(ProjectId projectId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(projectId.Value))
            throw new ArgumentException("ProjectId is required.", nameof(projectId));

        lock (_gate)
        {
            IReadOnlyList<AgentRunRecord> matches = _records
                .Where(r => string.Equals(r.ProjectId.Value, projectId.Value, StringComparison.Ordinal))
                .ToArray();
            return Task.FromResult(matches);
        }
    }

    public Task<IReadOnlyList<AgentRunRecord>> GetByCorrelationIdAsync(string correlationId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(correlationId))
            throw new ArgumentException("CorrelationId is required.", nameof(correlationId));

        lock (_gate)
        {
            IReadOnlyList<AgentRunRecord> matches = _records
                .Where(r => string.Equals(r.CorrelationId, correlationId, StringComparison.Ordinal))
                .ToArray();
            return Task.FromResult(matches);
        }
    }

    public Task<IReadOnlyList<AgentRunRecord>> GetRecentAsync(int maxCount, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (maxCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxCount), "maxCount must be positive.");

        lock (_gate)
        {
            IReadOnlyList<AgentRunRecord> matches = _records
                .AsEnumerable()
                .Reverse()
                .Take(maxCount)
                .ToArray();
            return Task.FromResult(matches);
        }
    }
}
