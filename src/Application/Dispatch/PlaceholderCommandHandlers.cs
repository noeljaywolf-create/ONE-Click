using OneClick.Application.Commands;
using OneClick.Application.Contracts;
using OneClick.Application.Persistence;
using OneClick.Core.Domain;
using OneClick.Core.TaggedValues;
using OneClick.Core.Units;

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

public sealed class ImportDocumentCommandHandler : ProjectCommandHandler<ImportDocumentCommand>
{
    private readonly IProjectDomainStateStore? _domainStateStore;

    public ImportDocumentCommandHandler(IProjectDomainStateStore? domainStateStore = null)
    {
        _domainStateStore = domainStateStore;
    }

    public override string CommandType => CommandTypes.ImportDocument;

    public override async Task<CommandHandlerResult> HandleAsync(
        ImportDocumentCommand command,
        CommandHandlerContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        if (_domainStateStore is null)
        {
            return new CommandHandlerResult(
                Array.Empty<string>(),
                Array.Empty<string>(),
                ["issue:domain-state-store:missing"]);
        }

        if (string.IsNullOrWhiteSpace(command.UploadHandle))
        {
            return new CommandHandlerResult(
                Array.Empty<string>(),
                Array.Empty<string>(),
                ["issue:import-document:upload-handle-required"]);
        }

        var state = await _domainStateStore.GetAsync(context.ProjectId, cancellationToken).ConfigureAwait(false);
        if (state is null)
        {
            return new CommandHandlerResult(
                Array.Empty<string>(),
                Array.Empty<string>(),
                [$"issue:project-not-found:{context.ProjectId.Value}"]);
        }

        var commandRef = $"command:{context.CommandId.Value}";
        var documentId = $"document:{context.CommandId.Value}";
        var regionId = $"region:{documentId}:default";
        var sourceRefs = new[] { commandRef, $"upload:{command.UploadHandle.Trim()}" };
        const string methodVersion = "import-document.v1";

        var region = new DocumentRegionRecord(regionId, documentId, sourceRefs, methodVersion);
        var document = new ImportedDocumentRecord(
            documentId,
            command.UploadHandle.Trim(),
            command.DocumentLabel,
            command.SourceFileName,
            [regionId],
            sourceRefs,
            methodVersion);

        state.UpsertImportedDocument(document);
        state.UpsertRegion(region);

        SyncRevision(state, context.ExpectedRevision);
        await _domainStateStore.SaveAsync(state, cancellationToken).ConfigureAwait(false);

        return new CommandHandlerResult(
            [documentId, regionId],
            Array.Empty<string>(),
            Array.Empty<string>());
    }

    private static void SyncRevision(ProjectDomainState state, ProjectRevision expectedRevision)
    {
        var nextRevision = expectedRevision.Value + 1;
        if (nextRevision > state.Revision)
            state.SetRevision(nextRevision);
    }
}

public sealed class SetCalibrationCommandHandler : ProjectCommandHandler<SetCalibrationCommand>
{
    private readonly IProjectDomainStateStore? _domainStateStore;

    public SetCalibrationCommandHandler(IProjectDomainStateStore? domainStateStore = null)
    {
        _domainStateStore = domainStateStore;
    }

    public override string CommandType => CommandTypes.SetCalibration;

    public override async Task<CommandHandlerResult> HandleAsync(
        SetCalibrationCommand command,
        CommandHandlerContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        if (_domainStateStore is null)
        {
            return new CommandHandlerResult(
                Array.Empty<string>(),
                Array.Empty<string>(),
                ["issue:domain-state-store:missing"]);
        }

        if (string.IsNullOrWhiteSpace(command.RegionId))
        {
            return new CommandHandlerResult(
                Array.Empty<string>(),
                Array.Empty<string>(),
                ["issue:set-calibration:region-id-required"]);
        }

        if (command.ControlPoints is null || command.ControlPoints.Count < 2)
        {
            return new CommandHandlerResult(
                Array.Empty<string>(),
                Array.Empty<string>(),
                ["issue:set-calibration:control-points-insufficient"]);
        }

        for (var i = 0; i < command.ControlPoints.Count; i++)
        {
            var p = command.ControlPoints[i];
            if (p is null
                || !double.IsFinite(p.SourceX) || !double.IsFinite(p.SourceY)
                || !double.IsFinite(p.ModelX) || !double.IsFinite(p.ModelY)
                || (p.ModelZ.HasValue && !double.IsFinite(p.ModelZ.Value)))
            {
                return new CommandHandlerResult(
                    Array.Empty<string>(),
                    Array.Empty<string>(),
                    [$"issue:set-calibration:control-point-invalid:{i}"]);
            }
        }

        var state = await _domainStateStore.GetAsync(context.ProjectId, cancellationToken).ConfigureAwait(false);
        if (state is null)
        {
            return new CommandHandlerResult(
                Array.Empty<string>(),
                Array.Empty<string>(),
                [$"issue:project-not-found:{context.ProjectId.Value}"]);
        }

        var regionId = command.RegionId.Trim();
        if (!state.TryGetRegion(regionId, out _))
        {
            return new CommandHandlerResult(
                Array.Empty<string>(),
                Array.Empty<string>(),
                [$"issue:set-calibration:region-not-found:{regionId}"]);
        }

        var hadExisting = state.TryGetCalibrationForRegion(regionId, out var existing);
        var calibrationId = $"calibration:{regionId}";
        var sourceRefs = new[]
        {
            $"command:{context.CommandId.Value}",
            $"region:{regionId}"
        };
        const string methodVersion = "set-calibration.v1";

        var controlPoints = command.ControlPoints
            .Select(p => new CalibrationControlPointRecord(p.SourceX, p.SourceY, p.ModelX, p.ModelY, p.ModelZ))
            .ToArray();

        var calibration = new CalibrationRecord(
            calibrationId,
            regionId,
            controlPoints,
            command.CalibrationNotes,
            sourceRefs,
            methodVersion);

        state.UpsertCalibration(calibration);
        SyncRevision(state, context.ExpectedRevision);
        await _domainStateStore.SaveAsync(state, cancellationToken).ConfigureAwait(false);

        var createdIds = hadExisting ? Array.Empty<string>() : new[] { calibrationId };
        var invalidatedIds = hadExisting && existing is not null
            ? new[] { existing.CalibrationId }
            : Array.Empty<string>();

        return new CommandHandlerResult(createdIds, invalidatedIds, Array.Empty<string>());
    }

    private static void SyncRevision(ProjectDomainState state, ProjectRevision expectedRevision)
    {
        var nextRevision = expectedRevision.Value + 1;
        if (nextRevision > state.Revision)
            state.SetRevision(nextRevision);
    }
}

public sealed class UpsertRouteCommandHandler : ProjectCommandHandler<UpsertRouteCommand>
{
    private readonly IProjectDomainStateStore? _domainStateStore;

    public UpsertRouteCommandHandler(IProjectDomainStateStore? domainStateStore = null)
    {
        _domainStateStore = domainStateStore;
    }

    public override string CommandType => CommandTypes.UpsertRoute;

    public override async Task<CommandHandlerResult> HandleAsync(
        UpsertRouteCommand command,
        CommandHandlerContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        if (_domainStateStore is null)
        {
            return new CommandHandlerResult(
                Array.Empty<string>(),
                Array.Empty<string>(),
                ["issue:domain-state-store:missing"]);
        }

        if (string.IsNullOrWhiteSpace(command.RouteId))
        {
            return new CommandHandlerResult(
                Array.Empty<string>(),
                Array.Empty<string>(),
                ["issue:upsert-route:route-id-required"]);
        }

        if (command.Vertices is null || command.Vertices.Count < 2)
        {
            return new CommandHandlerResult(
                Array.Empty<string>(),
                Array.Empty<string>(),
                ["issue:upsert-route:vertices-required"]);
        }

        var state = await _domainStateStore.GetAsync(context.ProjectId, cancellationToken).ConfigureAwait(false);
        if (state is null)
        {
            return new CommandHandlerResult(
                Array.Empty<string>(),
                Array.Empty<string>(),
                [$"issue:project-not-found:{context.ProjectId.Value}"]);
        }

        if (!state.TryGetPiece(command.RouteId, out var piece) || piece is null)
        {
            return new CommandHandlerResult(
                Array.Empty<string>(),
                Array.Empty<string>(),
                [$"issue:route-not-found:{command.RouteId}"]);
        }

        var startVertex = command.Vertices[0];
        var endVertex = command.Vertices[^1];

        var sourceRefs = new[] { $"command:{context.CommandId.Value}", $"piece:{piece.Id}" };
        var methodVersion = "upsert-route.v1";

        var startElevation = ToElevation(startVertex.Z, sourceRefs, methodVersion, piece.Id, "start");
        var endElevation = ToElevation(endVertex.Z, sourceRefs, methodVersion, piece.Id, "end");

        var updatedStartM = new OneClick.Core.Geometry.Point3D(startVertex.X, startVertex.Y, startZForPoint(piece.StartM.Z, startVertex.Z));
        var updatedEndM = new OneClick.Core.Geometry.Point3D(endVertex.X, endVertex.Y, startZForPoint(piece.EndM.Z, endVertex.Z));

        var updatedPiece = new RoutePiece(
            piece.Id,
            updatedStartM,
            updatedEndM,
            piece.StartNetOffsetM,
            piece.EndNetOffsetM,
            piece.Group,
            piece.SourceRefs,
            piece.MethodVersion,
            startElevation,
            endElevation);

        state.UpsertPiece(updatedPiece);

        // Sync domain revision with committed command revision.
        var nextRevision = context.ExpectedRevision.Value + 1;
        if (nextRevision > state.Revision)
            state.SetRevision(nextRevision);

        await _domainStateStore.SaveAsync(state, cancellationToken).ConfigureAwait(false);

        var invalidated = new List<string> { $"piece:{piece.Id}" };
        var issues = new List<string>();

        CollectElevationIssues(startElevation, piece.Id, "start", issues);
        CollectElevationIssues(endElevation, piece.Id, "end", issues);

        // Invalidate affected ledgers when elevation/length may have changed.
        invalidated.Add($"ledger:route:{piece.Id}");
        invalidated.Add($"ledger:cut:{piece.Id}");

        return new CommandHandlerResult(
            Array.Empty<string>(),
            invalidated,
            issues);
    }

    private static TaggedValue ToElevation(double? z, string[] sourceRefs, string methodVersion, string pieceId, string endName)
    {
        if (z.HasValue)
        {
            if (!double.IsFinite(z.Value))
                throw new ArgumentOutOfRangeException(nameof(z), z, "Elevation must be finite when provided.");
            return new KnownValue(z.Value, SiUnit.Meter, sourceRefs, methodVersion);
        }

        return new UnknownValue(
            SiUnit.Meter,
            sourceRefs,
            methodVersion,
            $"Elevation is unknown for {endName} of piece '{pieceId}'.",
            [$"issue:elevation-unknown:{pieceId}:{endName}"]);
    }

    private static double startZForPoint(double previousZ, double? commandedZ)
    {
        if (commandedZ.HasValue)
        {
            if (!double.IsFinite(commandedZ.Value))
                throw new ArgumentOutOfRangeException(nameof(commandedZ), commandedZ, "Elevation must be finite when provided.");
            return commandedZ.Value;
        }

        // Point3D cannot represent unknown; keep previous numeric Z but authoritative elevation is UnknownValue.
        return previousZ;
    }

    private static void CollectElevationIssues(TaggedValue elevation, string pieceId, string endName, List<string> issues)
    {
        if (elevation is UnknownValue unknown)
        {
            foreach (var issue in unknown.IssueRefs)
            {
                if (!issues.Contains(issue, StringComparer.Ordinal))
                    issues.Add(issue);
            }
        }
    }
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

public sealed class ReleaseSnapshotCommandHandler : ProjectCommandHandler<ReleaseSnapshotCommand>
{
    private readonly IProjectDomainStateStore? _domainStateStore;

    public ReleaseSnapshotCommandHandler(IProjectDomainStateStore? domainStateStore = null)
    {
        _domainStateStore = domainStateStore;
    }

    public override string CommandType => CommandTypes.ReleaseSnapshot;

    public override async Task<CommandHandlerResult> HandleAsync(
        ReleaseSnapshotCommand command,
        CommandHandlerContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        if (_domainStateStore is null)
        {
            return new CommandHandlerResult(
                Array.Empty<string>(),
                Array.Empty<string>(),
                ["issue:domain-state-store:missing"]);
        }

        var state = await _domainStateStore.GetAsync(context.ProjectId, cancellationToken).ConfigureAwait(false);
        if (state is null)
        {
            return new CommandHandlerResult(
                Array.Empty<string>(),
                Array.Empty<string>(),
                [$"issue:project-not-found:{context.ProjectId.Value}"]);
        }

        var manifest = state.SnapshotManifests.FirstOrDefault(m => string.Equals(m.SnapshotId, command.SnapshotId, StringComparison.Ordinal));
        if (manifest is null)
        {
            return new CommandHandlerResult(
                Array.Empty<string>(),
                Array.Empty<string>(),
                [$"issue:snapshot-not-found:{command.SnapshotId}"]);
        }

        var releaseId = $"release:{command.SnapshotId}:{context.CommandId.Value}";
        var issues = new List<string>();
        if (manifest.UnknownQuantities.Count > 0)
            issues.Add("issue:release:unknown-quantities-present");
        if (manifest.Issues.Count > 0)
            issues.Add("issue:release:snapshot-has-issues");

        return new CommandHandlerResult(
            [releaseId],
            Array.Empty<string>(),
            issues);
    }
}
