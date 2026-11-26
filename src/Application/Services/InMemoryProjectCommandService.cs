using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using OneClick.Application.Commands;
using OneClick.Application.Contracts;
using OneClick.Application.Errors;
using OneClick.Application.Validation;
using OneClick.Core.Ids;

namespace OneClick.Application.Services;

/// <summary>
/// Deterministic in-memory command execution skeleton. It enforces boundary validation,
/// expected-revision preconditions, and command replay semantics within process scope.
/// It does not perform real engineering mutations, persistence, or transport concerns.
/// </summary>
public sealed class InMemoryProjectCommandService : IProjectCommandService, IProjectQueryService
{
    private static readonly JsonSerializerOptions HashJsonOptions = new(JsonSerializerDefaults.Web);

    private static readonly IReadOnlyList<string> SupportedCommands =
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
    ];

    private readonly ICommandBoundaryValidator _validator;
    private readonly ConcurrentDictionary<string, ProjectState> _projects = new(StringComparer.Ordinal);
    private readonly object _gate = new();

    public InMemoryProjectCommandService(ICommandBoundaryValidator? validator = null)
    {
        _validator = validator ?? new CommandBoundaryValidator();
    }

    /// <summary>
    /// Creates an in-memory project at initial revision. Intended for skeleton/test bootstrap.
    /// </summary>
    public ProjectView CreateProject(ProjectId projectId)
    {
        if (string.IsNullOrWhiteSpace(projectId.Value))
        {
            throw new ApplicationProblemException(Malformed("ProjectId is required.", "projectId"));
        }

        lock (_gate)
        {
            if (_projects.ContainsKey(projectId.Value))
            {
                throw new ApplicationProblemException(Conflict(
                    $"Project '{projectId.Value}' already exists.",
                    "projectId"));
            }

            var state = new ProjectState(projectId.Value);
            _projects[projectId.Value] = state;
            return state.ToView();
        }
    }

    public async Task<CommandReceipt> ExecuteCommandAsync(
        ProjectId projectId,
        ProjectRevision expectedRevision,
        CommandId commandId,
        ProjectCommand command,
        ActorContext actorContext,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var validationRequest = new CommandValidationRequest(projectId, expectedRevision, commandId, command, actorContext);
        var validationResult = await _validator.ValidateAsync(validationRequest, context: null, cancellationToken).ConfigureAwait(false);
        if (!validationResult.IsValid)
        {
            throw new ApplicationProblemException(validationResult.Problems[0]);
        }

        var bodyHash = ComputeBodyHash(command);
        var replayKey = BuildReplayKey(projectId, actorContext, commandId);

        lock (_gate)
        {
            if (!_projects.TryGetValue(projectId.Value, out var state))
            {
                throw new ApplicationProblemException(NotFound(
                    $"Project '{projectId.Value}' was not found.",
                    "projectId"));
            }

            if (state.ReplayEntries.TryGetValue(replayKey, out var existing))
            {
                if (string.Equals(existing.BodyHash, bodyHash, StringComparison.Ordinal))
                {
                    return existing.Receipt;
                }

                throw new ApplicationProblemException(Conflict(
                    "Command replay key was already used with a different command body.",
                    "commandId",
                    state.Revision));
            }

            if (state.Revision != expectedRevision)
            {
                throw new ApplicationProblemException(StaleRevision(state.Revision));
            }

            var newRevision = new ProjectRevision(state.Revision.Value + 1);
            var receipt = new CommandReceipt(
                newRevision,
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>());

            state.Revision = newRevision;
            state.ReplayEntries[replayKey] = new ReplayEntry(bodyHash, receipt);

            return receipt;
        }
    }

    public Task<ProjectView> QueryProjectAsync(
        ProjectId projectId,
        ActorContext actorContext,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(actorContext);

        if (string.IsNullOrWhiteSpace(projectId.Value))
        {
            throw new ApplicationProblemException(Malformed("ProjectId is required.", "projectId"));
        }

        lock (_gate)
        {
            if (!_projects.TryGetValue(projectId.Value, out var state))
            {
                throw new ApplicationProblemException(NotFound(
                    $"Project '{projectId.Value}' was not found.",
                    "projectId"));
            }

            return Task.FromResult(state.ToView());
        }
    }

    private static string BuildReplayKey(ProjectId projectId, ActorContext actorContext, CommandId commandId)
    {
        // In-memory replay scope: project + actor session + command id.
        // The HTTP adapter contract also scopes by method/path; this skeleton has no transport.
        return string.Join("|",
            projectId.Value,
            actorContext.ActorId,
            actorContext.SessionId,
            commandId.Value);
    }

    private static string ComputeBodyHash(ProjectCommand command)
    {
        var json = JsonSerializer.Serialize(command, command.GetType(), HashJsonOptions);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        return Convert.ToHexString(bytes);
    }

    private static ApplicationProblemDetails Malformed(string detail, string field) =>
        new(
            ApplicationProblemTypes.MalformedPayload,
            "Malformed command payload",
            400,
            detail,
            $"urn:oneclick:validation:{field}",
            ApplicationProblemCodes.MalformedPayload,
            Array.Empty<string>(),
            null);

    private static ApplicationProblemDetails NotFound(string detail, string field) =>
        new(
            ApplicationProblemTypes.NotFound,
            "Resource not found",
            404,
            detail,
            $"urn:oneclick:validation:{field}",
            ApplicationProblemCodes.NotFound,
            Array.Empty<string>(),
            null);

    private static ApplicationProblemDetails Conflict(string detail, string field, ProjectRevision? currentRevision = null) =>
        new(
            ApplicationProblemTypes.Conflict,
            "Command conflict",
            409,
            detail,
            $"urn:oneclick:validation:{field}",
            ApplicationProblemCodes.Conflict,
            Array.Empty<string>(),
            currentRevision);

    private static ApplicationProblemDetails StaleRevision(ProjectRevision currentRevision) =>
        new(
            ApplicationProblemTypes.StaleRevision,
            "Stale project revision",
            412,
            "Expected revision does not match current project revision.",
            "urn:oneclick:validation:expectedRevision",
            ApplicationProblemCodes.StaleRevision,
            Array.Empty<string>(),
            currentRevision);

    private sealed class ProjectState(string projectId)
    {
        public string ProjectId { get; } = projectId;
        public ProjectRevision Revision { get; set; } = ProjectRevision.Initial;
        public Dictionary<string, ReplayEntry> ReplayEntries { get; } = new(StringComparer.Ordinal);

        public ProjectView ToView() =>
            new(
                ProjectId,
                Revision,
                new ProjectCapabilities(SupportedCommands, ["QueryProject"]),
                new ProjectScope(HasAcceptedModel: false, KnownLimitations: ["in-memory-skeleton"]));
    }

    private sealed record ReplayEntry(string BodyHash, CommandReceipt Receipt);
}
