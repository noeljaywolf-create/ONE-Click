using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using OneClick.Application.Commands;
using OneClick.Application.Contracts;
using OneClick.Application.Dispatch;
using OneClick.Application.Errors;
using OneClick.Application.Persistence;
using OneClick.Application.Persistence.InMemory;
using OneClick.Application.Validation;
using OneClick.Core.Ids;

namespace OneClick.Application.Services;

/// <summary>
/// Application command/query service orchestrating validation, dispatch, transaction, persistence, and outbox seams.
/// The default constructor wires process-local in-memory adapters for tests/skeleton use; those adapters are not durable.
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
    private readonly IProjectCommandStore _store;
    private readonly ICommandTransactionFactory _transactionFactory;
    private readonly ICommandOutboxWriter _outbox;
    private readonly ICommandDispatcher _dispatcher;
    private readonly IProjectDomainStateStore? _domainStateStore;

    public InMemoryProjectCommandService(ICommandBoundaryValidator? validator = null)
        : this(validator, domainStateStore: null)
    {
    }

    public InMemoryProjectCommandService(
        ICommandBoundaryValidator? validator,
        IProjectDomainStateStore? domainStateStore)
        : this(
            validator,
            CreateDefaultInMemoryStore(out var sync),
            new InMemoryCommandTransactionFactory(sync),
            new InMemoryCommandOutboxWriter(),
            CreateDefaultDispatcher(domainStateStore),
            domainStateStore)
    {
    }

    public InMemoryProjectCommandService(
        ICommandBoundaryValidator? validator,
        IProjectCommandStore store,
        ICommandTransactionFactory transactionFactory,
        ICommandOutboxWriter outbox,
        ICommandDispatcher dispatcher)
        : this(validator, store, transactionFactory, outbox, dispatcher, domainStateStore: null)
    {
    }

    public InMemoryProjectCommandService(
        ICommandBoundaryValidator? validator,
        IProjectCommandStore store,
        ICommandTransactionFactory transactionFactory,
        ICommandOutboxWriter outbox,
        ICommandDispatcher dispatcher,
        IProjectDomainStateStore? domainStateStore)
    {
        _validator = validator ?? new CommandBoundaryValidator();
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _transactionFactory = transactionFactory ?? throw new ArgumentNullException(nameof(transactionFactory));
        _outbox = outbox ?? throw new ArgumentNullException(nameof(outbox));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _domainStateStore = domainStateStore;
    }

    /// <summary>
    /// Creates an in-memory project at initial revision. Intended for skeleton/test bootstrap.
    /// </summary>
    public ProjectView CreateProject(ProjectId projectId)
    {
        _store.CreateProjectAsync(projectId).GetAwaiter().GetResult();
        if (_domainStateStore is not null)
        {
            var state = new OneClick.Core.Domain.ProjectDomainState(projectId, revision: 0);
            _domainStateStore.SaveAsync(state).GetAwaiter().GetResult();
        }
        return BuildView(projectId.Value, ProjectRevision.Initial);
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

        await using var transaction = await _transactionFactory.BeginAsync(cancellationToken).ConfigureAwait(false);

        var state = await _store.GetStateAsync(projectId, cancellationToken).ConfigureAwait(false);
        if (state is null)
        {
            throw new ApplicationProblemException(NotFound(
                $"Project '{projectId.Value}' was not found.",
                "projectId"));
        }

        var existingReplay = await _store.FindReplayAsync(projectId, replayKey, cancellationToken).ConfigureAwait(false);
        if (existingReplay is not null)
        {
            if (string.Equals(existingReplay.BodyHash, bodyHash, StringComparison.Ordinal))
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return existingReplay.Receipt;
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

        var dispatchResult = await _dispatcher.DispatchAsync(
            command,
            new CommandHandlerContext(projectId, expectedRevision, commandId, actorContext),
            cancellationToken).ConfigureAwait(false);

        var newRevision = new ProjectRevision(state.Revision.Value + 1);
        var receipt = new CommandReceipt(
            newRevision,
            dispatchResult.CreatedIds,
            dispatchResult.InvalidatedIds,
            dispatchResult.IssueIds);

        var commit = new AcceptedCommandCommit(
            projectId,
            expectedRevision,
            newRevision,
            commandId,
            replayKey,
            bodyHash,
            command,
            actorContext,
            receipt);

        var committedReceipt = await _store.CommitAcceptedCommandAsync(commit, cancellationToken).ConfigureAwait(false);

        await _outbox.WriteAsync(
            new CommandOutboxMessage(
                Guid.NewGuid().ToString("N"),
                projectId,
                committedReceipt.NewRevision,
                commandId,
                command.CommandType,
                DateTimeOffset.UtcNow,
                committedReceipt),
            cancellationToken).ConfigureAwait(false);

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return committedReceipt;
    }

    public async Task<ProjectView> QueryProjectAsync(
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

        var state = await _store.GetStateAsync(projectId, cancellationToken).ConfigureAwait(false);
        if (state is null)
        {
            throw new ApplicationProblemException(NotFound(
                $"Project '{projectId.Value}' was not found.",
                "projectId"));
        }

        return BuildView(state.ProjectId.Value, state.Revision);
    }

    private static ProjectView BuildView(string projectId, ProjectRevision revision) =>
        new(
            projectId,
            revision,
            new ProjectCapabilities(SupportedCommands, ["QueryProject"]),
            new ProjectScope(HasAcceptedModel: false, KnownLimitations: ["in-memory-skeleton"]));

    private static string BuildReplayKey(ProjectId projectId, ActorContext actorContext, CommandId commandId)
    {
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

    private static IProjectCommandStore CreateDefaultInMemoryStore(out InMemoryCommandSynchronization sync)
    {
        sync = new InMemoryCommandSynchronization();
        return new InMemoryProjectCommandStore(sync);
    }

    private static ICommandDispatcher CreateDefaultDispatcher(IProjectDomainStateStore? domainStateStore) =>
        new CommandDispatcher(
        [
            new ImportDocumentCommandHandler(),
            new SetCalibrationCommandHandler(),
            new UpsertRouteCommandHandler(domainStateStore),
            new PlaceComponentCommandHandler(),
            new ConnectPortsCommandHandler(),
            new AcceptProposalCommandHandler(),
            new ReviewPropertyCommandHandler(),
            new SelectAssemblyCommandHandler(),
            new ReleaseSnapshotCommandHandler(domainStateStore),
        ]);

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
}
