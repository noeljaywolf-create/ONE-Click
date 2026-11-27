namespace OneClick.Application.Agents;

/// <summary>
/// Convenience base class for typed local agents. Bridges typed execution to the non-generic
/// registry contract.
/// </summary>
/// <typeparam name="TInput">Typed input payload.</typeparam>
/// <typeparam name="TOutput">Typed output payload.</typeparam>
public abstract class LocalAgentBase<TInput, TOutput> : ILocalAgent<TInput, TOutput>
{
    protected LocalAgentBase(AgentDescriptor descriptor)
    {
        Descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));
    }

    public AgentDescriptor Descriptor { get; }

    public Type InputType => typeof(TInput);

    public Type OutputType => typeof(TOutput);

    public abstract Task<AgentResult<TOutput>> ExecuteAsync(
        AgentRequest<TInput> request,
        AgentRunContext context,
        CancellationToken cancellationToken = default);

    async Task<AgentResult<object?>> ILocalAgent.ExecuteAsync(
        AgentRequest<object?> request,
        AgentRunContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);

        if (request.Input is not TInput typedInput)
        {
            if (request.Input is null && default(TInput) is null)
            {
                typedInput = default!;
            }
            else
            {
                return AgentResult<object?>.Failure(
                    "agent.input.invalid_type",
                    $"Agent '{Descriptor.Id}' expected input type '{typeof(TInput).FullName}' but received '{request.Input?.GetType().FullName ?? "null"}'.");
            }
        }

        var typedResult = await ExecuteAsync(new AgentRequest<TInput>(typedInput), context, cancellationToken).ConfigureAwait(false);
        return MapResult(typedResult);
    }

    private static AgentResult<object?> MapResult(AgentResult<TOutput> result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return new AgentResult<object?>(
            result.IsSuccess,
            result.Output,
            result.Issues,
            result.Warnings,
            result.ProvenanceRefs,
            result.ErrorCode,
            result.ErrorDetail);
    }
}
