using OneClick.Application.Commands;
using OneClick.Core.Ids;

namespace OneClick.Application.Contracts;

/// <summary>
/// Application command boundary. Implementations validate preconditions, enforce invariants,
/// commit atomically, and return receipts. This scaffold does not include an execution engine.
/// </summary>
public interface IProjectCommandService
{
    Task<CommandReceipt> ExecuteCommandAsync(
        ProjectId projectId,
        ProjectRevision expectedRevision,
        CommandId commandId,
        ProjectCommand command,
        ActorContext actorContext,
        CancellationToken cancellationToken = default);
}
