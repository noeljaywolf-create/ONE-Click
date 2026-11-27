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
        var service = new InMemoryProjectCommandService();
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
        Assert.Empty(receipt.CreatedIds);
        Assert.Empty(receipt.InvalidatedIds);
        Assert.Equal(["issue:placeholder:ImportDocument"], receipt.IssueIds);

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
