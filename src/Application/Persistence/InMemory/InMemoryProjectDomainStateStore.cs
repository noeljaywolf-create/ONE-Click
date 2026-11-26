using OneClick.Core.Domain;
using OneClick.Core.Ids;

namespace OneClick.Application.Persistence.InMemory;

/// <summary>
/// Process-local in-memory domain state store for the first build slice. Not durable.
/// </summary>
public sealed class InMemoryProjectDomainStateStore : IProjectDomainStateStore
{
    private readonly object _gate = new();
    private readonly Dictionary<string, ProjectDomainState> _states = new(StringComparer.Ordinal);

    public Task<ProjectDomainState?> GetAsync(ProjectId projectId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(projectId.Value))
            throw new ArgumentException("ProjectId is required.", nameof(projectId));

        lock (_gate)
        {
            return Task.FromResult(_states.TryGetValue(projectId.Value, out var state) ? state : null);
        }
    }

    public Task<ProjectDomainSnapshot?> GetSnapshotAsync(ProjectId projectId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(projectId.Value))
            throw new ArgumentException("ProjectId is required.", nameof(projectId));

        lock (_gate)
        {
            return Task.FromResult(_states.TryGetValue(projectId.Value, out var state) ? state.ToSnapshot() : null);
        }
    }

    public Task SaveAsync(ProjectDomainState state, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(state);

        lock (_gate)
        {
            _states[state.ProjectId.Value] = state;
            return Task.CompletedTask;
        }
    }
}
