using OneClick.Application.Commands;

namespace OneClick.Application.Dispatch;

/// <summary>
/// Base class for typed command handlers. It performs the runtime type check and delegates to the typed method.
/// </summary>
public abstract class ProjectCommandHandler<TCommand> : IProjectCommandHandler<TCommand>
    where TCommand : ProjectCommand
{
    public abstract string CommandType { get; }

    public abstract Task<CommandHandlerResult> HandleAsync(
        TCommand command,
        CommandHandlerContext context,
        CancellationToken cancellationToken = default);

    async Task<CommandHandlerResult> IProjectCommandHandler.HandleAsync(
        ProjectCommand command,
        CommandHandlerContext context,
        CancellationToken cancellationToken)
    {
        if (command is not TCommand typed)
        {
            throw new ArgumentException(
                $"Handler '{GetType().Name}' expected '{typeof(TCommand).Name}' but received '{command?.GetType().Name ?? "<null>"}'.",
                nameof(command));
        }

        return await HandleAsync(typed, context, cancellationToken).ConfigureAwait(false);
    }
}
