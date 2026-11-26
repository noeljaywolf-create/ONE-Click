using OneClick.Application.Commands;

namespace OneClick.Application.Dispatch;

/// <summary>
/// Handles a specific typed command. Implementations must be contract-safe placeholders or
/// real handlers; they must not perform fake engineering mutations.
/// </summary>
public interface IProjectCommandHandler
{
    string CommandType { get; }

    Task<CommandHandlerResult> HandleAsync(
        ProjectCommand command,
        CommandHandlerContext context,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Typed command handler convenience interface.
/// </summary>
public interface IProjectCommandHandler<in TCommand> : IProjectCommandHandler
    where TCommand : ProjectCommand
{
    Task<CommandHandlerResult> HandleAsync(
        TCommand command,
        CommandHandlerContext context,
        CancellationToken cancellationToken = default);
}
