using OneClick.Application.Commands;

namespace OneClick.Application.Dispatch;

/// <summary>
/// Contract-safe placeholder handler. It records that the command was accepted at the boundary
/// but no engineering mutation was performed by this skeleton.
/// </summary>
public abstract class PlaceholderCommandHandler<TCommand> : ProjectCommandHandler<TCommand>
    where TCommand : ProjectCommand
{
    public override Task<CommandHandlerResult> HandleAsync(
        TCommand command,
        CommandHandlerContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(new CommandHandlerResult(
            Array.Empty<string>(),
            Array.Empty<string>(),
            [$"issue:placeholder:{CommandType}"]));
    }
}

public sealed class ImportDocumentCommandHandler : PlaceholderCommandHandler<ImportDocumentCommand>
{
    public override string CommandType => CommandTypes.ImportDocument;
}

public sealed class SetCalibrationCommandHandler : PlaceholderCommandHandler<SetCalibrationCommand>
{
    public override string CommandType => CommandTypes.SetCalibration;
}

public sealed class UpsertRouteCommandHandler : PlaceholderCommandHandler<UpsertRouteCommand>
{
    public override string CommandType => CommandTypes.UpsertRoute;
}

public sealed class PlaceComponentCommandHandler : PlaceholderCommandHandler<PlaceComponentCommand>
{
    public override string CommandType => CommandTypes.PlaceComponent;
}

public sealed class ConnectPortsCommandHandler : PlaceholderCommandHandler<ConnectPortsCommand>
{
    public override string CommandType => CommandTypes.ConnectPorts;
}

public sealed class AcceptProposalCommandHandler : PlaceholderCommandHandler<AcceptProposalCommand>
{
    public override string CommandType => CommandTypes.AcceptProposal;
}

public sealed class ReviewPropertyCommandHandler : PlaceholderCommandHandler<ReviewPropertyCommand>
{
    public override string CommandType => CommandTypes.ReviewProperty;
}

public sealed class SelectAssemblyCommandHandler : PlaceholderCommandHandler<SelectAssemblyCommand>
{
    public override string CommandType => CommandTypes.SelectAssembly;
}

public sealed class ReleaseSnapshotCommandHandler : PlaceholderCommandHandler<ReleaseSnapshotCommand>
{
    public override string CommandType => CommandTypes.ReleaseSnapshot;
}
