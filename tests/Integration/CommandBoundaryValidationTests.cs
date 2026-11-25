using OneClick.Application.Commands;
using OneClick.Application.Contracts;
using OneClick.Application.Errors;
using OneClick.Application.Validation;
using OneClick.Core.Ids;
using OneClick.Core.TaggedValues;

namespace OneClick.Tests.Integration;

public sealed class CommandBoundaryValidationTests
{
    private static readonly CommandBoundaryValidator Validator = new();

    [Fact]
    public async Task Valid_request_passes_structural_validation_without_context()
    {
        var request = CreateRequest(new ImportDocumentCommand("upload-1", "Plan", "a.pdf"));

        var result = await Validator.ValidateAsync(request);

        Assert.True(result.IsValid);
        Assert.Empty(result.Problems);
    }

    [Fact]
    public async Task Missing_actor_context_fails_with_malformed_payload()
    {
        var request = new CommandValidationRequest(
            ProjectId.New(),
            new ProjectRevision(1),
            new CommandId("cmd-1"),
            new ImportDocumentCommand("upload-1", null, null),
            null);

        var result = await Validator.ValidateAsync(request);

        var problem = Assert.Single(result.Problems);
        Assert.False(result.IsValid);
        Assert.Equal(ApplicationProblemCodes.MalformedPayload, problem.Code);
        Assert.Equal(ApplicationProblemTypes.MalformedPayload, problem.Type);
        Assert.Equal(400, problem.Status);
        Assert.Contains("ActorContext is required", problem.Detail);
    }

    [Fact]
    public async Task Missing_command_id_and_negative_revision_fail()
    {
        var request = new CommandValidationRequest(
            ProjectId.New(),
            new ProjectRevision(-1),
            new CommandId(" "),
            new ImportDocumentCommand("upload-1", null, null),
            new ActorContext("actor-1", "session-1"));

        var result = await Validator.ValidateAsync(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Problems, p => p.Detail == "CommandId is required.");
        Assert.Contains(result.Problems, p => p.Detail == "ExpectedRevision must be zero or greater.");
        Assert.All(result.Problems, p => Assert.Equal(ApplicationProblemCodes.MalformedPayload, p.Code));
    }

    [Fact]
    public async Task Unknown_command_type_fails()
    {
        var request = CreateRequest(new UnknownCommand());

        var result = await Validator.ValidateAsync(request);

        var problem = Assert.Single(result.Problems);
        Assert.Equal(ApplicationProblemCodes.MalformedPayload, problem.Code);
        Assert.Contains("not known", problem.Detail);
    }

    [Fact]
    public async Task Command_specific_required_fields_are_validated()
    {
        var request = CreateRequest(new ConnectPortsCommand("", " ", null));

        var result = await Validator.ValidateAsync(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Problems, p => p.Detail == "command.portAId is required.");
        Assert.Contains(result.Problems, p => p.Detail == "command.portBId is required.");
    }

    [Fact]
    public async Task Non_finite_numbers_and_invalid_enum_fail_structurally()
    {
        var badCalibration = new SetCalibrationCommand(
            "region-1",
            [new CalibrationControlPoint(double.NaN, 0, 0, 0, double.PositiveInfinity)],
            null);
        var badReview = new ReviewPropertyCommand("prop-1", (ReviewState)999, null);

        var calibrationResult = await Validator.ValidateAsync(CreateRequest(badCalibration));
        var reviewResult = await Validator.ValidateAsync(CreateRequest(badReview));

        Assert.False(calibrationResult.IsValid);
        Assert.Contains(calibrationResult.Problems, p => p.Detail!.Contains("sourceX") && p.Detail.Contains("finite"));
        Assert.Contains(calibrationResult.Problems, p => p.Detail!.Contains("modelZ") && p.Detail.Contains("finite"));

        var reviewProblem = Assert.Single(reviewResult.Problems);
        Assert.Contains("ReviewState", reviewProblem.Detail);
    }

    [Fact]
    public async Task Optional_strings_must_not_be_empty_when_provided()
    {
        var request = CreateRequest(new UpsertRouteCommand(" ", [new RouteVertex(0, 0, null)], "", null, null));

        var result = await Validator.ValidateAsync(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Problems, p => p.Detail == "command.routeId must not be empty when provided.");
        Assert.Contains(result.Problems, p => p.Detail == "command.startPortId must not be empty when provided.");
    }

    [Fact]
    public async Task Context_hook_reports_project_scope_missing_as_not_found()
    {
        var request = CreateRequest(new ImportDocumentCommand("upload-1", null, null));
        var context = new FakeValidationContext { ProjectExists = false };

        var result = await Validator.ValidateAsync(request, context);

        var problem = Assert.Single(result.Problems);
        Assert.Equal(ApplicationProblemCodes.NotFound, problem.Code);
        Assert.Equal(ApplicationProblemTypes.NotFound, problem.Type);
        Assert.Equal(404, problem.Status);
    }

    [Fact]
    public async Task Context_hook_reports_stale_expected_revision()
    {
        var request = CreateRequest(new ImportDocumentCommand("upload-1", null, null), expectedRevision: 3);
        var context = new FakeValidationContext { ProjectExists = true, CurrentRevision = new ProjectRevision(4) };

        var result = await Validator.ValidateAsync(request, context);

        var problem = Assert.Single(result.Problems);
        Assert.Equal(ApplicationProblemCodes.StaleRevision, problem.Code);
        Assert.Equal(ApplicationProblemTypes.StaleRevision, problem.Type);
        Assert.Equal(412, problem.Status);
        Assert.Equal(4, problem.CurrentRevision?.Value);
    }

    [Fact]
    public async Task Context_hook_reports_missing_project_scoped_reference()
    {
        var request = CreateRequest(new SetCalibrationCommand("region-missing", [], null));
        var context = new FakeValidationContext { ProjectExists = true, CurrentRevision = new ProjectRevision(1) };
        context.MissingReferences.Add(("region", "region-missing"));

        var result = await Validator.ValidateAsync(request, context);

        var problem = Assert.Single(result.Problems);
        Assert.Equal(ApplicationProblemCodes.NotFound, problem.Code);
        Assert.Equal(404, problem.Status);
        Assert.Contains("region-missing", problem.Detail);
    }

    [Fact]
    public async Task Structural_failures_skip_context_hooks()
    {
        var request = new CommandValidationRequest(
            ProjectId.New(),
            new ProjectRevision(1),
            new CommandId("cmd-1"),
            new ImportDocumentCommand("", null, null),
            new ActorContext("actor-1", "session-1"));
        var context = new TrackingValidationContext();

        var result = await Validator.ValidateAsync(request, context);

        Assert.False(result.IsValid);
        Assert.Equal(0, context.ProjectExistsCalls);
        Assert.Equal(0, context.GetCurrentRevisionCalls);
    }

    private static CommandValidationRequest CreateRequest(ProjectCommand command, long expectedRevision = 1) =>
        new(
            ProjectId.New(),
            new ProjectRevision(expectedRevision),
            new CommandId("cmd-1"),
            command,
            new ActorContext("actor-1", "session-1"));

    private sealed record UnknownCommand : ProjectCommand
    {
        public override string CommandType => "UnknownCommand";
    }

    private sealed class FakeValidationContext : ICommandValidationContext
    {
        public bool ProjectExists { get; set; } = true;
        public ProjectRevision? CurrentRevision { get; set; }
        public HashSet<(string Kind, string Id)> MissingReferences { get; } = new();

        public Task<bool> ProjectExistsAsync(ProjectId projectId, CancellationToken cancellationToken = default) =>
            Task.FromResult(ProjectExists);

        public Task<ProjectRevision?> GetCurrentRevisionAsync(ProjectId projectId, CancellationToken cancellationToken = default) =>
            Task.FromResult(CurrentRevision);

        public Task<bool> ReferenceExistsInProjectAsync(ProjectId projectId, string referenceKind, string referenceId, CancellationToken cancellationToken = default) =>
            Task.FromResult(!MissingReferences.Contains((referenceKind, referenceId)));
    }

    private sealed class TrackingValidationContext : ICommandValidationContext
    {
        public int ProjectExistsCalls { get; private set; }
        public int GetCurrentRevisionCalls { get; private set; }

        public Task<bool> ProjectExistsAsync(ProjectId projectId, CancellationToken cancellationToken = default)
        {
            ProjectExistsCalls++;
            return Task.FromResult(true);
        }

        public Task<ProjectRevision?> GetCurrentRevisionAsync(ProjectId projectId, CancellationToken cancellationToken = default)
        {
            GetCurrentRevisionCalls++;
            return Task.FromResult<ProjectRevision?>(new ProjectRevision(1));
        }

        public Task<bool> ReferenceExistsInProjectAsync(ProjectId projectId, string referenceKind, string referenceId, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);
    }
}
