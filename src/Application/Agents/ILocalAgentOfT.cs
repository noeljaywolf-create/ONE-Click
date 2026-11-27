namespace OneClick.Application.Agents;

/// <summary>
/// Typed local agent execution contract.
/// </summary>
/// <typeparam name="TInput">Typed input payload.</typeparam>
/// <typeparam name="TOutput">Typed output payload.</typeparam>
public interface ILocalAgent<TInput, TOutput> : ILocalAgent
{
    Task<AgentResult<TOutput>> ExecuteAsync(
        AgentRequest<TInput> request,
        AgentRunContext context,
        CancellationToken cancellationToken = default);
}
