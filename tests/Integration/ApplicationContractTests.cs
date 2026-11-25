using System.Text.Json;
using OneClick.Application.Commands;
using OneClick.Application.Contracts;
using OneClick.Application.Errors;
using OneClick.Core.Ids;
using OneClick.Core.TaggedValues;

namespace OneClick.Tests.Integration;

public sealed class ApplicationContractTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public void ExecuteCommand_Interface_Exposes_Required_Contract_Shape()
    {
        var method = typeof(IProjectCommandService).GetMethod(nameof(IProjectCommandService.ExecuteCommandAsync));
        Assert.NotNull(method);

        var parameters = method!.GetParameters();
        Assert.Equal(typeof(ProjectId), parameters[0].ParameterType);
        Assert.Equal(typeof(ProjectRevision), parameters[1].ParameterType);
        Assert.Equal(typeof(CommandId), parameters[2].ParameterType);
        Assert.Equal(typeof(ProjectCommand), parameters[3].ParameterType);
        Assert.Equal(typeof(ActorContext), parameters[4].ParameterType);
        Assert.Equal(typeof(CancellationToken), parameters[5].ParameterType);
        Assert.Equal(typeof(Task<CommandReceipt>), method.ReturnType);
    }

    [Fact]
    public void QueryProject_Interface_Exposes_Required_Contract_Shape()
    {
        var method = typeof(IProjectQueryService).GetMethod(nameof(IProjectQueryService.QueryProjectAsync));
        Assert.NotNull(method);

        var parameters = method!.GetParameters();
        Assert.Equal(typeof(ProjectId), parameters[0].ParameterType);
        Assert.Equal(typeof(ActorContext), parameters[1].ParameterType);
        Assert.Equal(typeof(CancellationToken), parameters[2].ParameterType);
        Assert.Equal(typeof(Task<ProjectView>), method.ReturnType);
    }

    [Fact]
    public void ActorContext_Is_Session_Based_Contract()
    {
        var actor = new ActorContext("actor-1", "session-1", "Operator");

        Assert.Equal("actor-1", actor.ActorId);
        Assert.Equal("session-1", actor.SessionId);
        Assert.Equal("Operator", actor.DisplayName);
    }

    [Fact]
    public void CommandReceipt_Reports_Revision_And_Affected_Ids()
    {
        var receipt = new CommandReceipt(
            new ProjectRevision(7),
            ["comp-1"],
            ["qty-1"],
            ["issue-1"]);

        Assert.Equal(7, receipt.NewRevision.Value);
        Assert.Equal(["comp-1"], receipt.CreatedIds);
        Assert.Equal(["qty-1"], receipt.InvalidatedIds);
        Assert.Equal(["issue-1"], receipt.IssueIds);
    }

    [Fact]
    public void Initial_Command_Union_Has_Stable_Command_Types()
    {
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

        Assert.Equal(
            [
                CommandTypes.ImportDocument,
                CommandTypes.SetCalibration,
                CommandTypes.UpsertRoute,
                CommandTypes.PlaceComponent,
                CommandTypes.ConnectPorts,
                CommandTypes.AcceptProposal,
                CommandTypes.ReviewProperty,
                CommandTypes.SelectAssembly,
                CommandTypes.ReleaseSnapshot,
            ],
            commands.Select(c => c.CommandType).ToArray());
    }

    [Fact]
    public void Command_Union_RoundTrips_Through_SystemTextJson()
    {
        ProjectCommand command = new ReviewPropertyCommand("prop-1", ReviewState.ClarificationRequired, "needs evidence", "issue-9");

        var json = JsonSerializer.Serialize(command, JsonOptions);
        Assert.StartsWith("{\"commandType\":", json);

        var roundTrip = JsonSerializer.Deserialize<ProjectCommand>(json, JsonOptions);

        var typed = Assert.IsType<ReviewPropertyCommand>(roundTrip);
        Assert.Equal("prop-1", typed.PropertyId);
        Assert.Equal(ReviewState.ClarificationRequired, typed.ReviewState);
        Assert.Equal("issue-9", typed.IssueId);
    }

    [Fact]
    public void ProblemDetails_Contract_Carries_Rfc9457_And_Project_Extensions()
    {
        var problem = new ApplicationProblemDetails(
            ApplicationProblemTypes.StaleRevision,
            "Stale project revision",
            412,
            "Expected revision does not match current project revision.",
            "urn:oneclick:instance:cmd-1",
            ApplicationProblemCodes.StaleRevision,
            ["issue-7"],
            new ProjectRevision(8));

        var json = JsonSerializer.Serialize(problem, JsonOptions);
        var roundTrip = JsonSerializer.Deserialize<ApplicationProblemDetails>(json, JsonOptions);

        Assert.NotNull(roundTrip);
        Assert.Equal(412, roundTrip!.Status);
        Assert.Equal(ApplicationProblemCodes.StaleRevision, roundTrip.Code);
        Assert.Equal(8, roundTrip.CurrentRevision?.Value);
        Assert.Equal(["issue-7"], roundTrip.IssueIds);
        Assert.Contains("\"type\":\"urn:oneclick:problem:stale-revision\"", json);
    }

    [Fact]
    public void ApplicationResult_Placeholder_Represents_Success_And_Failure()
    {
        var success = ApplicationResult<CommandReceipt>.Success(
            new CommandReceipt(new ProjectRevision(2), [], [], []));
        var failure = ApplicationResult<CommandReceipt>.Failure(
            new ApplicationProblemDetails(
                ApplicationProblemTypes.PreconditionRequired,
                "Precondition required",
                428,
                "If-Match is required.",
                null,
                ApplicationProblemCodes.PreconditionRequired,
                [],
                new ProjectRevision(2)));

        Assert.True(success.IsSuccess);
        Assert.NotNull(success.Value);
        Assert.Null(success.Problem);

        Assert.False(failure.IsSuccess);
        Assert.Null(failure.Value);
        Assert.Equal(428, failure.Problem?.Status);
    }
}
