namespace OneClick.Application.Agents;

/// <summary>
/// Typed input envelope for local agent execution.
/// </summary>
/// <typeparam name="TInput">Typed input payload.</typeparam>
public sealed record AgentRequest<TInput>(TInput Input)
{
    public static AgentRequest<TInput> From(TInput input) => new(input);
}
