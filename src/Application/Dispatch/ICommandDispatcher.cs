using OneClick.Application.Commands;

namespace OneClick.Application.Dispatch;

/// <summary>
/// Dispatches a typed command to a registered handler.
/// </summary>
public interface ICommandDispatcher
{
    Task<CommandHandlerResult> DispatchAsync(
        ProjectCommand command,
        CommandHandlerContext context,
        CancellationToken cancellationToken = default);
}
