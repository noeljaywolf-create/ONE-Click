namespace OneClick.Application.Agents;

/// <summary>
/// Non-generic contract for local agent discovery and execution. Implementations must be
/// local/offline and should be deterministic for the same inputs and versions.
/// </summary>
public interface ILocalAgent
{
    AgentDescriptor Descriptor { get; }

    Type InputType { get; }

    Type OutputType { get; }

    Task<AgentResult<object?>> ExecuteAsync(
        AgentRequest<object?> request,
        AgentRunContext context,
        CancellationToken cancellationToken = default);
}
