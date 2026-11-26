using OneClick.Application.Commands;
using OneClick.Application.Contracts;
using OneClick.Application.Dispatch;
using OneClick.Application.Errors;
using OneClick.Core.Ids;
using OneClick.Core.TaggedValues;

namespace OneClick.Tests.Integration;

public sealed class CommandDispatchTests
{
    [Fact]
    public async Task Dispatcher_Routes_To_Registered_Handler()
    {
        var handler = new RecordingHandler(CommandTypes.ImportDocument);
        var dispatcher = new CommandDispatcher([handler]);
        var context = CreateContext();

        var result = await dispatcher.DispatchAsync(
            new ImportDocumentCommand("upload-1", null, null),
            context);

        Assert.Same(handler.LastResult, result);
        Assert.Equal(CommandTypes.ImportDocument, handler.LastCommand?.CommandType);
    }

    [Fact]
    public async Task Dispatcher_Throws_Problem_For_Unregistered_Command()
    {
        var dispatcher = new CommandDispatcher([]);
        var context = CreateContext();

        var ex = await Assert.ThrowsAsync<ApplicationProblemException>(() =>
            dispatcher.DispatchAsync(new ImportDocumentCommand("upload-1", null, null), context));

        Assert.Equal(400, ex.Problem.Status);
        Assert.Equal(ApplicationProblemCodes.MalformedPayload, ex.Problem.Code);
        Assert.Contains("ImportDocument", ex.Problem.Detail);
    }

    [Fact]
    public async Task Placeholder_Handlers_Return_Structured_Issue_Without_Mutations()
    {
        var handlers = new IProjectCommandHandler[]
        {
            new ImportDocumentCommandHandler(),
            new SetCalibrationCommandHandler(),
            new UpsertRouteCommandHandler(),
            new PlaceComponentCommandHandler(),
            new ConnectPortsCommandHandler(),
            new AcceptProposalCommandHandler(),
            new ReviewPropertyCommandHandler(),
            new SelectAssemblyCommandHandler(),
            new ReleaseSnapshotCommandHandler(),
        };

        var dispatcher = new CommandDispatcher(handlers);
        var context = CreateContext();

        ProjectCommand[] commands =
        [
            new ImportDocumentCommand("upload-1", "Plan", "a.pdf"),
            new SetCalibrationCommand("region-1", [new CalibrationControlPoint(0, 0, 0, 0, null)], null),
            new UpsertRouteCommand(null, [new RouteVertex(0, 0, null), new RouteVertex(1, 1, null)], null, null, null),
            new PlaceComponentCommand(null, "part-1", "rel-1", new ComponentPlacement(1, 2, null, 0), null),
            new ConnectPortsCommand("port-a", "port-b", null),
            new AcceptProposalCommand("proposal-1", null),
            new ReviewPropertyCommand("prop-1", ReviewState.Accepted, "ok"),
            new SelectAssemblyCommand("ctx-1", "cand-1", null),
            new ReleaseSnapshotCommand("snap-1", "takeoff-release", null),
        ];

        foreach (var command in commands)
        {
            var result = await dispatcher.DispatchAsync(command, context);

            Assert.Empty(result.CreatedIds);
            Assert.Empty(result.InvalidatedIds);
            Assert.Equal([$"issue:placeholder:{command.CommandType}"], result.IssueIds);
        }
    }

    [Fact]
    public void Dispatcher_Rejects_Duplicate_Handlers()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            new CommandDispatcher([
                new ImportDocumentCommandHandler(),
                new ImportDocumentCommandHandler(),
            ]));

        Assert.Contains("Duplicate", ex.Message);
    }

    private static CommandHandlerContext CreateContext() =>
        new(ProjectId.New(), ProjectRevision.Initial, new CommandId("cmd-1"), new ActorContext("actor-1", "session-1"));

    private sealed class RecordingHandler : IProjectCommandHandler
    {
        public RecordingHandler(string commandType)
        {
            CommandType = commandType;
            LastResult = new CommandHandlerResult([], [], []);
        }

        public string CommandType { get; }
        public ProjectCommand? LastCommand { get; private set; }
        public CommandHandlerResult LastResult { get; }

        public Task<CommandHandlerResult> HandleAsync(ProjectCommand command, CommandHandlerContext context, CancellationToken cancellationToken = default)
        {
            LastCommand = command;
            return Task.FromResult(LastResult);
        }
    }
}
