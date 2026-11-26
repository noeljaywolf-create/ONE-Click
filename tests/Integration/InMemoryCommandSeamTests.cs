using OneClick.Application.Commands;
using OneClick.Application.Contracts;
using OneClick.Application.Dispatch;
using OneClick.Application.Errors;
using OneClick.Application.Persistence;
using OneClick.Application.Persistence.InMemory;
using OneClick.Application.Services;
using OneClick.Core.Ids;

namespace OneClick.Tests.Integration;

public sealed class InMemoryCommandSeamTests
{
    [Fact]
    public async Task Store_Create_Get_Commit_And_Replay_Behavior()
    {
        var sync = new InMemoryCommandSynchronization();
        var store = new InMemoryProjectCommandStore(sync);
        var projectId = ProjectId.New();

        await store.CreateProjectAsync(projectId);
        var state = await store.GetStateAsync(projectId);

        Assert.NotNull(state);
        Assert.Equal(ProjectRevision.Initial, state!.Revision);

        var command = new ImportDocumentCommand("upload-1", null, null);
        var commit = new AcceptedCommandCommit(
            projectId,
            ProjectRevision.Initial,
            new ProjectRevision(1),
            new CommandId("cmd-1"),
            "replay-key",
            "hash-a",
            command,
            new ActorContext("actor-1", "session-1"),
            new CommandReceipt(new ProjectRevision(1), [], [], []));

        var receipt = await store.CommitAcceptedCommandAsync(commit);
        Assert.Equal(1, receipt.NewRevision.Value);

        var replay = await store.FindReplayAsync(projectId, "replay-key");
        Assert.NotNull(replay);
        Assert.Equal("hash-a", replay!.BodyHash);
        Assert.Equal(receipt, replay.Receipt);

        var after = await store.GetStateAsync(projectId);
        Assert.Equal(1, after!.Revision.Value);
    }

    [Fact]
    public async Task Store_Commit_Rejects_Stale_ExpectedRevision()
    {
        var sync = new InMemoryCommandSynchronization();
        var store = new InMemoryProjectCommandStore(sync);
        var projectId = ProjectId.New();
        await store.CreateProjectAsync(projectId);

        var command = new ImportDocumentCommand("upload-1", null, null);
        await store.CommitAcceptedCommandAsync(new AcceptedCommandCommit(
            projectId,
            ProjectRevision.Initial,
            new ProjectRevision(1),
            new CommandId("cmd-1"),
            "rk-1",
            "hash-a",
            command,
            new ActorContext("actor-1", "session-1"),
            new CommandReceipt(new ProjectRevision(1), [], [], [])));

        var ex = await Assert.ThrowsAsync<ApplicationProblemException>(() =>
            store.CommitAcceptedCommandAsync(new AcceptedCommandCommit(
                projectId,
                ProjectRevision.Initial,
                new ProjectRevision(1),
                new CommandId("cmd-2"),
                "rk-2",
                "hash-b",
                command,
                new ActorContext("actor-1", "session-1"),
                new CommandReceipt(new ProjectRevision(1), [], [], []))));

        Assert.Equal(ApplicationProblemCodes.StaleRevision, ex.Problem.Code);
        Assert.Equal(1, ex.Problem.CurrentRevision?.Value);
    }

    [Fact]
    public async Task OutboxWriter_Records_Messages_InMemory()
    {
        var outbox = new InMemoryCommandOutboxWriter();
        var message = new CommandOutboxMessage(
            "msg-1",
            ProjectId.New(),
            new ProjectRevision(1),
            new CommandId("cmd-1"),
            CommandTypes.ImportDocument,
            DateTimeOffset.UtcNow,
            new CommandReceipt(new ProjectRevision(1), [], [], []));

        await outbox.WriteAsync(message);

        Assert.Single(outbox.Messages);
        Assert.Equal("msg-1", outbox.Messages.First().MessageId);
    }

    [Fact]
    public async Task Service_Composed_With_Seams_Writes_Outbox_And_Uses_Dispatch()
    {
        var sync = new InMemoryCommandSynchronization();
        var store = new InMemoryProjectCommandStore(sync);
        var tx = new InMemoryCommandTransactionFactory(sync);
        var outbox = new InMemoryCommandOutboxWriter();
        var dispatcher = new CommandDispatcher([new ImportDocumentCommandHandler()]);
        var service = new InMemoryProjectCommandService(null, store, tx, outbox, dispatcher);

        var projectId = ProjectId.New();
        service.CreateProject(projectId);
        var actor = new ActorContext("actor-1", "session-1");

        var receipt = await service.ExecuteCommandAsync(
            projectId,
            ProjectRevision.Initial,
            new CommandId("cmd-1"),
            new ImportDocumentCommand("upload-1", null, null),
            actor);

        Assert.Equal(1, receipt.NewRevision.Value);
        Assert.Equal(["issue:placeholder:ImportDocument"], receipt.IssueIds);
        Assert.Single(outbox.Messages);
        Assert.Equal(CommandTypes.ImportDocument, outbox.Messages.First().CommandType);
        Assert.Equal(receipt, outbox.Messages.First().Receipt);
    }

    [Fact]
    public async Task Service_Replay_Does_Not_Write_Duplicate_Outbox_Message()
    {
        var sync = new InMemoryCommandSynchronization();
        var store = new InMemoryProjectCommandStore(sync);
        var tx = new InMemoryCommandTransactionFactory(sync);
        var outbox = new InMemoryCommandOutboxWriter();
        var dispatcher = new CommandDispatcher([new ImportDocumentCommandHandler()]);
        var service = new InMemoryProjectCommandService(null, store, tx, outbox, dispatcher);

        var projectId = ProjectId.New();
        service.CreateProject(projectId);
        var actor = new ActorContext("actor-1", "session-1");
        var command = new ImportDocumentCommand("upload-1", null, null);
        var commandId = new CommandId("cmd-1");

        var first = await service.ExecuteCommandAsync(projectId, ProjectRevision.Initial, commandId, command, actor);
        var replay = await service.ExecuteCommandAsync(projectId, new ProjectRevision(99), commandId, command, actor);

        Assert.Equal(first, replay);
        Assert.Single(outbox.Messages);
    }
}
