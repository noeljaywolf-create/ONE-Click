using OneClick.Application.Commands;
using OneClick.Application.Contracts;
using OneClick.Application.Dispatch;
using OneClick.Application.Persistence;
using OneClick.Core.Domain;
using OneClick.Core.Ids;

namespace OneClick.Application.Agents.Takeoff;

/// <summary>
/// Input for deterministic local document import metadata registration.
/// Wraps <see cref="ImportDocumentCommandHandler"/> behavior; no PDF parsing or content extraction.
/// </summary>
/// <param name="ProjectId">Target project identity.</param>
/// <param name="CommandId">Stable command identity used for deterministic document/region IDs and provenance.</param>
/// <param name="UploadHandle">Opaque staged upload handle/reference.</param>
/// <param name="DocumentLabel">Optional human label.</param>
/// <param name="SourceFileName">Optional source file name metadata.</param>
public sealed record ImportDocumentInput(
    ProjectId ProjectId,
    CommandId CommandId,
    string UploadHandle,
    string? DocumentLabel = null,
    string? SourceFileName = null);

/// <summary>
/// Typed output from deterministic document import metadata registration.
/// </summary>
/// <param name="ProjectId">Target project identity.</param>
/// <param name="Revision">Project revision after successful save.</param>
/// <param name="DocumentId">Created document ID.</param>
/// <param name="RegionId">Created default region ID.</param>
/// <param name="CreatedIds">IDs reported by the command handler.</param>
/// <param name="IssueIds">Issue IDs reported by the command handler.</param>
/// <param name="SourceRefs">Source/provenance refs recorded on the document metadata.</param>
public sealed record ImportDocumentOutput(
    ProjectId ProjectId,
    long Revision,
    string DocumentId,
    string RegionId,
    IReadOnlyList<string> CreatedIds,
    IReadOnlyList<string> IssueIds,
    IReadOnlyList<string> SourceRefs);

/// <summary>
/// Deterministic local agent that registers imported document metadata and a default region identity.
/// It wraps existing <see cref="ImportDocumentCommandHandler"/> and in-memory domain state store behavior.
/// Metadata/refs only - no PDF parsing, no geometry, no network/AI calls.
/// </summary>
public sealed class ImportDocumentAgent : LocalAgentBase<ImportDocumentInput, ImportDocumentOutput>
{
    public const string AgentIdValue = "takeoff.document.import";
    private const string MethodVersion = "import-document.v1";

    public static readonly AgentDescriptor AgentDescriptor = new(
        new AgentId(AgentIdValue),
        AgentKind.Deterministic,
        "Import Document",
        "Registers imported document metadata and a default region identity in local project domain state.",
        "1.0.0");

    private readonly IProjectDomainStateStore _domainStateStore;
    private readonly ImportDocumentCommandHandler _handler;

    public ImportDocumentAgent(IProjectDomainStateStore domainStateStore)
        : base(AgentDescriptor)
    {
        _domainStateStore = domainStateStore ?? throw new ArgumentNullException(nameof(domainStateStore));
        _handler = new ImportDocumentCommandHandler(domainStateStore);
    }

    public override async Task<AgentResult<ImportDocumentOutput>> ExecuteAsync(
        AgentRequest<ImportDocumentInput> request,
        AgentRunContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        var input = request.Input;
        if (input is null)
        {
            return AgentResult<ImportDocumentOutput>.Failure(
                "agent.input.required",
                "Import document input is required.");
        }

        if (string.IsNullOrWhiteSpace(input.ProjectId.Value))
        {
            return AgentResult<ImportDocumentOutput>.Failure(
                "import-document.project_id.required",
                "ProjectId is required.");
        }

        if (string.IsNullOrWhiteSpace(input.CommandId.Value))
        {
            return AgentResult<ImportDocumentOutput>.Failure(
                "import-document.command_id.required",
                "CommandId is required for deterministic document metadata registration.");
        }

        if (string.IsNullOrWhiteSpace(input.UploadHandle))
        {
            return AgentResult<ImportDocumentOutput>.Failure(
                "import-document.upload_handle.required",
                "UploadHandle is required.");
        }

        try
        {
            var state = await _domainStateStore.GetAsync(input.ProjectId, cancellationToken).ConfigureAwait(false);
            if (state is null)
            {
                return AgentResult<ImportDocumentOutput>.Failure(
                    "import-document.project_not_found",
                    $"Project domain state was not found for '{input.ProjectId.Value}'.",
                    issues: [$"issue:project-not-found:{input.ProjectId.Value}"]);
            }

            var command = new ImportDocumentCommand(input.UploadHandle, input.DocumentLabel, input.SourceFileName);
            var handlerContext = new CommandHandlerContext(
                input.ProjectId,
                new ProjectRevision(state.Revision),
                input.CommandId,
                context.Actor);

            var handlerResult = await _handler.HandleAsync(command, handlerContext, cancellationToken).ConfigureAwait(false);
            var issueIds = Normalize(handlerResult.IssueIds);

            if (issueIds.Count > 0)
            {
                return AgentResult<ImportDocumentOutput>.Failure(
                    "import-document.rejected",
                    $"ImportDocument was rejected: {string.Join(", ", issueIds)}",
                    issues: issueIds,
                    provenanceRefs: BuildProvenance(input, state.Revision, MethodVersion, issueIds));
            }

            var saved = await _domainStateStore.GetAsync(input.ProjectId, cancellationToken).ConfigureAwait(false);
            var revision = saved?.Revision ?? state.Revision;
            var documentId = $"document:{input.CommandId.Value}";
            var regionId = $"region:{documentId}:default";
            var sourceRefs = new[]
            {
                $"command:{input.CommandId.Value}",
                $"upload:{input.UploadHandle.Trim()}"
            };

            var output = new ImportDocumentOutput(
                input.ProjectId,
                revision,
                documentId,
                regionId,
                Normalize(handlerResult.CreatedIds),
                issueIds,
                sourceRefs);

            return AgentResult<ImportDocumentOutput>.Success(
                output,
                provenanceRefs: BuildProvenance(input, revision, MethodVersion, sourceRefs));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return AgentResult<ImportDocumentOutput>.Failure(
                "import-document.failed",
                $"Failed to register imported document metadata: {ex.Message}");
        }
    }

    private static IReadOnlyList<string> BuildProvenance(
        ImportDocumentInput input,
        long revision,
        string methodVersion,
        IReadOnlyList<string> refs)
    {
        var provenance = new List<string>
        {
            $"project:{input.ProjectId.Value}",
            $"revision:{revision}",
            $"command:{input.CommandId.Value}",
            $"method:{methodVersion}"
        };
        provenance.AddRange(refs.Where(r => !string.IsNullOrWhiteSpace(r)));
        return provenance.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static IReadOnlyList<string> Normalize(IReadOnlyList<string>? values)
    {
        if (values is null || values.Count == 0)
            return Array.Empty<string>();

        return values
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }
}
