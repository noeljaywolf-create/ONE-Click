using OneClick.Application.Agents;
using OneClick.Application.Agents.Takeoff;
using OneClick.Application.Persistence;

namespace OneClick.Desktop.Services;

/// <summary>
/// Desktop-owned composition and execution wrapper for local/offline takeoff agents.
/// Records recent run metadata for UI inspection; does not add cloud/LLM behavior.
/// </summary>
public sealed class DesktopAgentRuntimeService
{
    private readonly ILocalAgentRegistry _registry;
    private readonly List<AgentRunRecord> _recentRuns = [];
    private readonly object _gate = new();
    private readonly int _maxRecentRuns;

    public DesktopAgentRuntimeService(
        IProjectDomainStateStore domainStateStore,
        int maxRecentRuns = 20)
    {
        ArgumentNullException.ThrowIfNull(domainStateStore);
        if (maxRecentRuns <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxRecentRuns), "Max recent runs must be positive.");

        _maxRecentRuns = maxRecentRuns;
        _registry = TakeoffAgentRegistration.CreateTakeoffRegistry(domainStateStore);
    }

    public DesktopAgentRuntimeService(
        ILocalAgentRegistry registry,
        int maxRecentRuns = 20)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        if (maxRecentRuns <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxRecentRuns), "Max recent runs must be positive.");

        _maxRecentRuns = maxRecentRuns;
    }

    public ILocalAgentRegistry Registry => _registry;

    public IReadOnlyList<AgentDescriptor> GetDescriptors() => _registry.GetDescriptors();

    public IReadOnlyList<AgentRunRecord> GetRecentRuns()
    {
        lock (_gate)
        {
            return _recentRuns.ToArray();
        }
    }

    public async Task<AgentResult<object?>> ExecuteAsync(
        AgentId agentId,
        AgentRequest<object?> request,
        AgentRunContext context,
        CancellationToken cancellationToken = default)
    {
        var startedAt = DateTimeOffset.UtcNow;
        var result = await _registry.ExecuteAsync(agentId, request, context, cancellationToken).ConfigureAwait(false);
        Record(agentId, context, startedAt, DateTimeOffset.UtcNow, result.IsSuccess, result.ErrorCode, result.Issues.Count, result.Warnings.Count, result.ProvenanceRefs.Count);
        return result;
    }

    public async Task<AgentResult<TOutput>> ExecuteAsync<TInput, TOutput>(
        AgentId agentId,
        AgentRequest<TInput> request,
        AgentRunContext context,
        CancellationToken cancellationToken = default)
    {
        var startedAt = DateTimeOffset.UtcNow;
        var result = await _registry.ExecuteAsync<TInput, TOutput>(agentId, request, context, cancellationToken).ConfigureAwait(false);
        Record(agentId, context, startedAt, DateTimeOffset.UtcNow, result.IsSuccess, result.ErrorCode, result.Issues.Count, result.Warnings.Count, result.ProvenanceRefs.Count);
        return result;
    }

    private void Record(
        AgentId agentId,
        AgentRunContext context,
        DateTimeOffset startedAt,
        DateTimeOffset finishedAt,
        bool isSuccess,
        string? errorCode,
        int issueCount,
        int warningCount,
        int provenanceRefCount)
    {
        var descriptor = _registry.TryGet(agentId, out var agent) && agent is not null
            ? agent.Descriptor
            : new AgentDescriptor(agentId, AgentKind.Deterministic, agentId.Value, "Unregistered agent", "unknown");

        var record = AgentRunRecord.FromResult(
            context.CorrelationId,
            descriptor,
            startedAt,
            finishedAt,
            isSuccess,
            errorCode,
            issueCount,
            warningCount,
            provenanceRefCount);

        lock (_gate)
        {
            _recentRuns.Insert(0, record);
            if (_recentRuns.Count > _maxRecentRuns)
                _recentRuns.RemoveRange(_maxRecentRuns, _recentRuns.Count - _maxRecentRuns);
        }
    }
}
