namespace OneClick.Application.Agents;

/// <summary>
/// Registry and execution surface for local/offline agents.
/// </summary>
public interface ILocalAgentRegistry
{
    void Register(ILocalAgent agent);

    bool TryGet(AgentId agentId, out ILocalAgent? agent);

    IReadOnlyList<AgentDescriptor> GetDescriptors();

    Task<AgentResult<object?>> ExecuteAsync(
        AgentId agentId,
        AgentRequest<object?> request,
        AgentRunContext context,
        CancellationToken cancellationToken = default);

    Task<AgentResult<TOutput>> ExecuteAsync<TInput, TOutput>(
        AgentId agentId,
        AgentRequest<TInput> request,
        AgentRunContext context,
        CancellationToken cancellationToken = default);
}
