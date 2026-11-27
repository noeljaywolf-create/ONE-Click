using OneClick.Application.Commands;
using OneClick.Application.Contracts;
using OneClick.Application.Errors;
using OneClick.Application.Persistence.InMemory;
using OneClick.Application.Services;
using OneClick.Core.Domain;
using OneClick.Core.Geometry;
using OneClick.Core.Ids;

namespace OneClick.Tests.Integration;

public sealed class InMemoryProjectCommandServiceTests
{
    [Fact]
    public async Task ExecuteCommand_Succeeds_And_Increments_Revision()
    {
        var domainStore = new InMemoryProjectDomainStateStore();
        var service = new InMemoryProjectCommandService(validator: null, domainStateStore: domainStore);
        var projectId = ProjectId.New();
        service.CreateProject(projectId);

        var actor = new ActorContext("actor-1", "session-1");
        var command = new ImportDocumentCommand("upload-1", "Plan", "a.pdf");

        var receipt = await service.ExecuteCommandAsync(
            projectId,
            ProjectRevision.Initial,
            new CommandId("cmd-1"),
            command,
            actor);

        Assert.Equal(1, receipt.NewRevision.Value);
        Assert.Equal(["document:cmd-1", "region:document:cmd-1:default"], receipt.CreatedIds);
        Assert.Empty(receipt.InvalidatedIds);
        Assert.Empty(receipt.IssueIds);

        var view = await service.QueryProjectAsync(projectId, actor);
        Assert.Equal(1, view.Revision.Value);
        Assert.Equal(projectId.Value, view.ProjectId);
    }

    [Fact]
    public async Task ExecuteCommand_StaleRevision_Fails_With_412()
    {
        var service = new InMemoryProjectCommandService();
        var projectId = ProjectId.New();
        service.CreateProject(projectId);

        var actor = new ActorContext("actor-1", "session-1");
        var command = new ImportDocumentCommand("upload-1", null, null);

        await service.ExecuteCommandAsync(
            projectId,
            ProjectRevision.Initial,
            new CommandId("cmd-1"),
            command,
            actor);

        var ex = await Assert.ThrowsAsync<ApplicationProblemException>(() =>
            service.ExecuteCommandAsync(
                projectId,
                ProjectRevision.Initial,
                new CommandId("cmd-2"),
                command,
                actor));

        Assert.Equal(ApplicationProblemCodes.StaleRevision, ex.Problem.Code);
        Assert.Equal(ApplicationProblemTypes.StaleRevision, ex.Problem.Type);
        Assert.Equal(412, ex.Problem.Status);
        Assert.Equal(1, ex.Problem.CurrentRevision?.Value);
    }

    [Fact]
    public async Task ExecuteCommand_ReplaySameBody_Returns_Original_Receipt()
    {
        var service = new InMemoryProjectCommandService();
        var projectId = ProjectId.New();
        service.CreateProject(projectId);

        var actor = new ActorContext("actor-1", "session-1");
        var command = new ImportDocumentCommand("upload-1", "Plan", "a.pdf");
        var commandId = new CommandId("cmd-replay");

        var first = await service.ExecuteCommandAsync(
            projectId,
            ProjectRevision.Initial,
            commandId,
            command,
            actor);

        var replay = await service.ExecuteCommandAsync(
            projectId,
            new ProjectRevision(99),
            commandId,
            new ImportDocumentCommand("upload-1", "Plan", "a.pdf"),
            actor);

        Assert.Equal(first, replay);
        Assert.Equal(1, replay.NewRevision.Value);

        var view = await service.QueryProjectAsync(projectId, actor);
        Assert.Equal(1, view.Revision.Value);
    }

    [Fact]
    public async Task ExecuteCommand_ReplayDifferentBody_Fails_With_409()
    {
        var service = new InMemoryProjectCommandService();
        var projectId = ProjectId.New();
        service.CreateProject(projectId);

        var actor = new ActorContext("actor-1", "session-1");
        var commandId = new CommandId("cmd-replay");

        await service.ExecuteCommandAsync(
            projectId,
            ProjectRevision.Initial,
            commandId,
            new ImportDocumentCommand("upload-1", "Plan", "a.pdf"),
            actor);

        var ex = await Assert.ThrowsAsync<ApplicationProblemException>(() =>
            service.ExecuteCommandAsync(
                projectId,
                new ProjectRevision(1),
                commandId,
                new ImportDocumentCommand("upload-2", "Plan", "b.pdf"),
                actor));

        Assert.Equal(ApplicationProblemCodes.Conflict, ex.Problem.Code);
        Assert.Equal(ApplicationProblemTypes.Conflict, ex.Problem.Type);
        Assert.Equal(409, ex.Problem.Status);
        Assert.Equal(1, ex.Problem.CurrentRevision?.Value);
    }

    [Fact]
    public async Task QueryProject_Returns_Revision_Counts_And_Limitations()
    {
        var domainStore = new InMemoryProjectDomainStateStore();
        var service = new InMemoryProjectCommandService(validator: null, domainStateStore: domainStore);
        var projectId = ProjectId.New();
        service.CreateProject(projectId);
        var actor = new ActorContext("actor-1", "session-1");

        var empty = await service.QueryProjectAsync(projectId, actor);
        Assert.Equal(0, empty.Revision.Value);
        Assert.Equal(projectId.Value, empty.ProjectId);
        Assert.False(empty.Scope.HasAcceptedModel);
        Assert.Contains("in-memory-skeleton", empty.Scope.KnownLimitations);
        Assert.Equal(0, empty.Scope.PieceCount);
        Assert.Equal(0, empty.Scope.SnapshotCount);

        var state = await domainStore.GetAsync(projectId);
        Assert.NotNull(state);
        state!.UpsertPiece(CreatePiece("M1"));
        state.UpsertPiece(CreatePiece("B1"));
        await domainStore.SaveAsync(state);

        var withPieces = await service.QueryProjectAsync(projectId, actor);
        Assert.Equal(2, withPieces.Scope.PieceCount);
        Assert.Equal(0, withPieces.Scope.SnapshotCount);
        Assert.Contains("in-memory-skeleton", withPieces.Scope.KnownLimitations);
    }

    [Fact]
    public async Task QueryProject_Reports_SnapshotCount_From_DomainState()
    {
        var domainStore = new InMemoryProjectDomainStateStore();
        var service = new InMemoryProjectCommandService(validator: null, domainStateStore: domainStore);
        var projectId = ProjectId.New();
        service.CreateProject(projectId);
        var actor = new ActorContext("actor-1", "session-1");

        var state = await domainStore.GetAsync(projectId);
        Assert.NotNull(state);
        state!.AddSnapshotManifest(new SnapshotManifest(
            snapshotId: "snap-1",
            projectId: projectId.Value,
            projectRevision: 0,
            createdAtUtc: DateTimeOffset.UtcNow,
            label: "test"));
        await domainStore.SaveAsync(state);

        var view = await service.QueryProjectAsync(projectId, actor);
        Assert.Equal(0, view.Scope.PieceCount);
        Assert.Equal(1, view.Scope.SnapshotCount);
    }

    [Fact]
    public async Task UpsertRoute_Receipt_Contains_Affected_Piece_And_Ledger_Ids()
    {
        var domainStore = new InMemoryProjectDomainStateStore();
        var service = new InMemoryProjectCommandService(validator: null, domainStateStore: domainStore);
        var projectId = ProjectId.New();
        service.CreateProject(projectId);
        var actor = new ActorContext("actor-1", "session-1");

        var state = await domainStore.GetAsync(projectId);
        Assert.NotNull(state);
        state!.UpsertPiece(CreatePiece("M1"));
        await domainStore.SaveAsync(state);

        var receipt = await service.ExecuteCommandAsync(
            projectId,
            ProjectRevision.Initial,
            new CommandId("cmd-upsert-1"),
            new UpsertRouteCommand(
                "M1",
                [new RouteVertex(0, 0, 0), new RouteVertex(1, 0, 0)],
                null,
                null,
                null),
            actor);

        Assert.Equal(1, receipt.NewRevision.Value);
        Assert.Empty(receipt.CreatedIds);
        Assert.Contains("piece:M1", receipt.InvalidatedIds);
        Assert.Contains("ledger:route:M1", receipt.InvalidatedIds);
        Assert.Contains("ledger:cut:M1", receipt.InvalidatedIds);
        Assert.Empty(receipt.IssueIds);
    }

    [Fact]
    public async Task UpsertRoute_Receipt_Contains_ElevationUnknown_IssueIds_Consistently()
    {
        var domainStore = new InMemoryProjectDomainStateStore();
        var service = new InMemoryProjectCommandService(validator: null, domainStateStore: domainStore);
        var projectId = ProjectId.New();
        service.CreateProject(projectId);
        var actor = new ActorContext("actor-1", "session-1");

        var state = await domainStore.GetAsync(projectId);
        Assert.NotNull(state);
        state!.UpsertPiece(CreatePiece("M1"));
        await domainStore.SaveAsync(state);

        var receipt = await service.ExecuteCommandAsync(
            projectId,
            ProjectRevision.Initial,
            new CommandId("cmd-upsert-unknown"),
            new UpsertRouteCommand(
                "M1",
                [new RouteVertex(0, 0, null), new RouteVertex(1, 0, 1)],
                null,
                null,
                null),
            actor);

        Assert.Equal(1, receipt.NewRevision.Value);
        Assert.Contains("piece:M1", receipt.InvalidatedIds);
        Assert.Contains("ledger:route:M1", receipt.InvalidatedIds);
        Assert.Contains("ledger:cut:M1", receipt.InvalidatedIds);
        Assert.Contains("issue:elevation-unknown:M1:start", receipt.IssueIds);
        Assert.Single(receipt.IssueIds);
    }

    [Fact]
    public async Task UpsertRoute_Receipt_Contains_Both_ElevationUnknown_IssueIds_When_Both_Ends_Unknown()
    {
        var domainStore = new InMemoryProjectDomainStateStore();
        var service = new InMemoryProjectCommandService(validator: null, domainStateStore: domainStore);
        var projectId = ProjectId.New();
        service.CreateProject(projectId);
        var actor = new ActorContext("actor-1", "session-1");

        var state = await domainStore.GetAsync(projectId);
        Assert.NotNull(state);
        state!.UpsertPiece(CreatePiece("M1"));
        await domainStore.SaveAsync(state);

        var receipt = await service.ExecuteCommandAsync(
            projectId,
            ProjectRevision.Initial,
            new CommandId("cmd-upsert-both-unknown"),
            new UpsertRouteCommand(
                "M1",
                [new RouteVertex(0, 0, null), new RouteVertex(1, 0, null)],
                null,
                null,
                null),
            actor);

        Assert.Contains("issue:elevation-unknown:M1:start", receipt.IssueIds);
        Assert.Contains("issue:elevation-unknown:M1:end", receipt.IssueIds);
        Assert.Equal(2, receipt.IssueIds.Count);
    }

    [Fact]
    public async Task ImportDocument_Creates_Document_And_Region_Metadata()
    {
        var domainStore = new InMemoryProjectDomainStateStore();
        var service = new InMemoryProjectCommandService(validator: null, domainStateStore: domainStore);
        var projectId = ProjectId.New();
        service.CreateProject(projectId);
        var actor = new ActorContext("actor-1", "session-1");

        var receipt = await service.ExecuteCommandAsync(
            projectId,
            ProjectRevision.Initial,
            new CommandId("cmd-import-1"),
            new ImportDocumentCommand("upload-1", "Level 1 Plan", "level1.pdf"),
            actor);

        Assert.Equal(1, receipt.NewRevision.Value);
        Assert.Equal(["document:cmd-import-1", "region:document:cmd-import-1:default"], receipt.CreatedIds);
        Assert.Empty(receipt.InvalidatedIds);
        Assert.Empty(receipt.IssueIds);

        var state = await domainStore.GetAsync(projectId);
        Assert.NotNull(state);
        Assert.True(state!.TryGetImportedDocument("document:cmd-import-1", out var document));
        Assert.NotNull(document);
        Assert.Equal("upload-1", document!.UploadHandle);
        Assert.Equal("Level 1 Plan", document.DocumentLabel);
        Assert.Equal("level1.pdf", document.SourceFileName);
        Assert.Equal(["region:document:cmd-import-1:default"], document.RegionIds);
        Assert.Equal("import-document.v1", document.MethodVersion);
        Assert.Contains("command:cmd-import-1", document.SourceRefs);

        Assert.True(state.TryGetRegion("region:document:cmd-import-1:default", out var region));
        Assert.NotNull(region);
        Assert.Equal("document:cmd-import-1", region!.DocumentId);
    }

    [Fact]
    public async Task SetCalibration_MissingRegion_Returns_Clear_Issue_And_DoesNotMutateCalibration()
    {
        var domainStore = new InMemoryProjectDomainStateStore();
        var service = new InMemoryProjectCommandService(validator: null, domainStateStore: domainStore);
        var projectId = ProjectId.New();
        service.CreateProject(projectId);
        var actor = new ActorContext("actor-1", "session-1");

        var receipt = await service.ExecuteCommandAsync(
            projectId,
            ProjectRevision.Initial,
            new CommandId("cmd-cal-1"),
            new SetCalibrationCommand(
                "region:missing",
                [new CalibrationControlPoint(0, 0, 0, 0, null), new CalibrationControlPoint(1, 0, 1, 0, null)],
                "note"),
            actor);

        Assert.Equal(1, receipt.NewRevision.Value);
        Assert.Empty(receipt.CreatedIds);
        Assert.Empty(receipt.InvalidatedIds);
        Assert.Equal(["issue:set-calibration:region-not-found:region:missing"], receipt.IssueIds);

        var state = await domainStore.GetAsync(projectId);
        Assert.NotNull(state);
        Assert.Empty(state!.Calibrations);
    }

    [Fact]
    public async Task SetCalibration_Stores_Metadata_And_Updates_Existing_Calibration()
    {
        var domainStore = new InMemoryProjectDomainStateStore();
        var service = new InMemoryProjectCommandService(validator: null, domainStateStore: domainStore);
        var projectId = ProjectId.New();
        service.CreateProject(projectId);
        var actor = new ActorContext("actor-1", "session-1");

        await service.ExecuteCommandAsync(
            projectId,
            ProjectRevision.Initial,
            new CommandId("cmd-import-1"),
            new ImportDocumentCommand("upload-1", "Plan", "a.pdf"),
            actor);

        var regionId = "region:document:cmd-import-1:default";
        var first = await service.ExecuteCommandAsync(
            projectId,
            new ProjectRevision(1),
            new CommandId("cmd-cal-1"),
            new SetCalibrationCommand(
                regionId,
                [new CalibrationControlPoint(0, 0, 0, 0, null), new CalibrationControlPoint(10, 0, 10, 0, 0)],
                "initial"),
            actor);

        Assert.Equal(2, first.NewRevision.Value);
        Assert.Equal(["calibration:" + regionId], first.CreatedIds);
        Assert.Empty(first.InvalidatedIds);
        Assert.Empty(first.IssueIds);

        var second = await service.ExecuteCommandAsync(
            projectId,
            new ProjectRevision(2),
            new CommandId("cmd-cal-2"),
            new SetCalibrationCommand(
                regionId,
                [new CalibrationControlPoint(0, 0, 0, 0, null), new CalibrationControlPoint(20, 0, 20, 0, null)],
                "updated"),
            actor);

        Assert.Equal(3, second.NewRevision.Value);
        Assert.Empty(second.CreatedIds);
        Assert.Equal(["calibration:" + regionId], second.InvalidatedIds);
        Assert.Empty(second.IssueIds);

        var state = await domainStore.GetAsync(projectId);
        Assert.NotNull(state);
        Assert.True(state!.TryGetCalibrationForRegion(regionId, out var calibration));
        Assert.NotNull(calibration);
        Assert.Equal("calibration:" + regionId, calibration!.CalibrationId);
        Assert.Equal(regionId, calibration.RegionId);
        Assert.Equal("updated", calibration.Notes);
        Assert.Equal(2, calibration.ControlPoints.Count);
        Assert.Equal(20d, calibration.ControlPoints[1].SourceX);
        Assert.Equal("set-calibration.v1", calibration.MethodVersion);
        Assert.Contains("command:cmd-cal-2", calibration.SourceRefs);
        Assert.Contains("region:" + regionId, calibration.SourceRefs);
    }

    [Fact]
    public async Task SetCalibration_InsufficientControlPoints_Returns_Clear_Issue()
    {
        var domainStore = new InMemoryProjectDomainStateStore();
        var service = new InMemoryProjectCommandService(validator: null, domainStateStore: domainStore);
        var projectId = ProjectId.New();
        service.CreateProject(projectId);
        var actor = new ActorContext("actor-1", "session-1");

        await service.ExecuteCommandAsync(
            projectId,
            ProjectRevision.Initial,
            new CommandId("cmd-import-1"),
            new ImportDocumentCommand("upload-1", null, null),
            actor);

        var receipt = await service.ExecuteCommandAsync(
            projectId,
            new ProjectRevision(1),
            new CommandId("cmd-cal-1"),
            new SetCalibrationCommand(
                "region:document:cmd-import-1:default",
                [new CalibrationControlPoint(0, 0, 0, 0, null)],
                null),
            actor);

        Assert.Equal(2, receipt.NewRevision.Value);
        Assert.Empty(receipt.CreatedIds);
        Assert.Empty(receipt.InvalidatedIds);
        Assert.Equal(["issue:set-calibration:control-points-insufficient"], receipt.IssueIds);

        var state = await domainStore.GetAsync(projectId);
        Assert.NotNull(state);
        Assert.Empty(state!.Calibrations);
    }

    private static RoutePiece CreatePiece(string id) =>
        new(
            id: id,
            startM: new Point3D(0, 0, 0),
            endM: new Point3D(1, 0, 0),
            startNetOffsetM: 0,
            endNetOffsetM: 0,
            group: "main",
            sourceRefs: ["test"],
            methodVersion: "test.v1");
}
