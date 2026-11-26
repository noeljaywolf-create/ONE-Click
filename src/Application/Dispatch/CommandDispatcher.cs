using OneClick.Application.Commands;
using OneClick.Application.Errors;

namespace OneClick.Application.Dispatch;

/// <summary>
/// Simple in-process command dispatcher. It resolves handlers by command type string.
/// </summary>
public sealed class CommandDispatcher : ICommandDispatcher
{
    private readonly IReadOnlyDictionary<string, IProjectCommandHandler> _handlers;

    public CommandDispatcher(IEnumerable<IProjectCommandHandler> handlers)
    {
        ArgumentNullException.ThrowIfNull(handlers);

        var map = new Dictionary<string, IProjectCommandHandler>(StringComparer.Ordinal);
        foreach (var handler in handlers)
        {
            ArgumentNullException.ThrowIfNull(handler);
            if (string.IsNullOrWhiteSpace(handler.CommandType))
            {
                throw new ArgumentException("Handler command type is required.", nameof(handlers));
            }

            if (!map.TryAdd(handler.CommandType, handler))
            {
                throw new ArgumentException(
                    $"Duplicate command handler registered for '{handler.CommandType}'.",
                    nameof(handlers));
            }
        }

        _handlers = map;
    }

    public Task<CommandHandlerResult> DispatchAsync(
        ProjectCommand command,
        CommandHandlerContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        if (!_handlers.TryGetValue(command.CommandType, out var handler))
        {
            throw new ApplicationProblemException(new ApplicationProblemDetails(
                ApplicationProblemTypes.MalformedPayload,
                "Unsupported command type",
                400,
                $"No command handler is registered for '{command.CommandType}'.",
                $"urn:oneclick:command:{command.CommandType}",
                ApplicationProblemCodes.MalformedPayload,
                Array.Empty<string>(),
                null));
        }

        return handler.HandleAsync(command, context, cancellationToken);
    }
}
