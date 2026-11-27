using System.Collections.Concurrent;

namespace OneClick.Application.Agents;

/// <summary>
/// Simple in-memory registry/runner for local agents. Intended for local/offline execution and
/// deterministic composition in tests and first-slice workflows.
/// </summary>
public sealed class InMemoryLocalAgentRegistry : ILocalAgentRegistry
{
    private readonly ConcurrentDictionary<string, ILocalAgent> _agents = new(StringComparer.Ordinal);

    public void Register(ILocalAgent agent)
    {
        ArgumentNullException.ThrowIfNull(agent);
        ArgumentNullException.ThrowIfNull(agent.Descriptor);

        var id = agent.Descriptor.Id.Value;
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("Agent descriptor id is required.", nameof(agent));

        if (!_agents.TryAdd(id, agent))
            throw new InvalidOperationException($"Local agent '{id}' is already registered.");
    }

    public bool TryGet(AgentId agentId, out ILocalAgent? agent)
    {
        if (string.IsNullOrWhiteSpace(agentId.Value))
        {
            agent = null;
            return false;
        }

        return _agents.TryGetValue(agentId.Value, out agent);
    }

    public IReadOnlyList<AgentDescriptor> GetDescriptors()
    {
        return _agents.Values
            .Select(a => a.Descriptor)
            .OrderBy(d => d.Id.Value, StringComparer.Ordinal)
            .ToArray();
    }

    public async Task<AgentResult<object?>> ExecuteAsync(
        AgentId agentId,
        AgentRequest<object?> request,
        AgentRunContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);

        if (string.IsNullOrWhiteSpace(agentId.Value))
        {
            return AgentResult<object?>.Failure(
                "agent.id.required",
                "Agent id is required.");
        }

        if (!_agents.TryGetValue(agentId.Value, out var agent))
        {
            return AgentResult<object?>.Failure(
                "agent.not_found",
                $"Local agent '{agentId.Value}' is not registered.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        return await agent.ExecuteAsync(request, context, cancellationToken).ConfigureAwait(false);
    }

    public async Task<AgentResult<TOutput>> ExecuteAsync<TInput, TOutput>(
        AgentId agentId,
        AgentRequest<TInput> request,
        AgentRunContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);

        if (string.IsNullOrWhiteSpace(agentId.Value))
        {
            return AgentResult<TOutput>.Failure(
                "agent.id.required",
                "Agent id is required.");
        }

        if (!_agents.TryGetValue(agentId.Value, out var agent))
        {
            return AgentResult<TOutput>.Failure(
                "agent.not_found",
                $"Local agent '{agentId.Value}' is not registered.");
        }

        cancellationToken.ThrowIfCancellationRequested();

        if (agent is ILocalAgent<TInput, TOutput> typedAgent)
        {
            return await typedAgent.ExecuteAsync(request, context, cancellationToken).ConfigureAwait(false);
        }

        var untyped = await agent.ExecuteAsync(new AgentRequest<object?>(request.Input), context, cancellationToken).ConfigureAwait(false);
        return MapUntypedResult<TOutput>(agentId, untyped);
    }

    private static AgentResult<TOutput> MapUntypedResult<TOutput>(AgentId agentId, AgentResult<object?> result)
    {
        if (!result.IsSuccess)
        {
            return AgentResult<TOutput>.Failure(
                result.ErrorCode ?? "agent.execution.failed",
                result.ErrorDetail ?? $"Local agent '{agentId.Value}' execution failed.",
                result.Issues,
                result.Warnings,
                result.ProvenanceRefs);
        }

        if (result.Output is TOutput typedOutput)
        {
            return AgentResult<TOutput>.Success(typedOutput, result.Issues, result.Warnings, result.ProvenanceRefs);
        }

        if (result.Output is null && default(TOutput) is null)
        {
            return AgentResult<TOutput>.Success(default!, result.Issues, result.Warnings, result.ProvenanceRefs);
        }

        return AgentResult<TOutput>.Failure(
            "agent.output.invalid_type",
            $"Local agent '{agentId.Value}' returned output type '{result.Output?.GetType().FullName ?? "null"}' but '{typeof(TOutput).FullName}' was requested.",
            result.Issues,
            result.Warnings,
            result.ProvenanceRefs);
    }
}
