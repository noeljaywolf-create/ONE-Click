using OneClick.Application.Commands;
using OneClick.Application.Contracts;
using OneClick.Application.Errors;
using OneClick.Application.Services;
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
}
