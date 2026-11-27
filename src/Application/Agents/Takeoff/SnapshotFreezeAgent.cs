using OneClick.Application.Persistence;
using OneClick.Application.Services;
using OneClick.Core.Domain;
using OneClick.Core.Ids;

namespace OneClick.Application.Agents.Takeoff;

/// <summary>
/// Input for deterministic snapshot freeze, with optional export artifact generation.
/// </summary>
/// <param name="ProjectId">Target project identity.</param>
/// <param name="SnapshotId">Stable snapshot identifier.</param>
/// <param name="CreatedAtUtc">Snapshot creation timestamp (UTC).</param>
/// <param name="Label">Optional human label.</param>
/// <param name="Exclusions">Optional exclusion markers.</param>
/// <param name="ExportJson">When true, generates a JSON export artifact via <see cref="SnapshotExportService"/>.</param>
/// <param name="ExportCsv">When true, generates a CSV export artifact via <see cref="SnapshotExportService"/>.</param>
public sealed record SnapshotFreezeInput(
    ProjectId ProjectId,
    string SnapshotId,
    DateTimeOffset CreatedAtUtc,
    string? Label = null,
    IReadOnlyList<string>? Exclusions = null,
    bool ExportJson = false,
    bool ExportCsv = false);

/// <summary>
/// Typed output from snapshot freeze agent.
/// </summary>
public sealed record SnapshotFreezeOutput(
    ProjectId ProjectId,
    string SnapshotId,
    long ProjectRevision,
    SnapshotManifest Manifest,
    SnapshotExportArtifact? JsonExport,
    SnapshotExportArtifact? CsvExport);

/// <summary>
/// Deterministic local agent that freezes current project domain state into a snapshot manifest.
/// Uses <see cref="SnapshotFreezeService"/> for freeze and <see cref="SnapshotExportService"/>
/// for optional export artifact generation. No network/AI calls.
/// </summary>
public sealed class SnapshotFreezeAgent : LocalAgentBase<SnapshotFreezeInput, SnapshotFreezeOutput>
{
    public static readonly AgentDescriptor AgentDescriptor = new(
        new AgentId("takeoff.snapshot.freeze"),
        AgentKind.Deterministic,
        "Snapshot Freeze",
        "Freezes project domain state into an immutable snapshot manifest with optional deterministic export artifacts.",
        "1.0.0");

    private readonly SnapshotFreezeService _freezeService;

    public SnapshotFreezeAgent(SnapshotFreezeService freezeService)
        : base(AgentDescriptor)
    {
        _freezeService = freezeService ?? throw new ArgumentNullException(nameof(freezeService));
    }

    public override async Task<AgentResult<SnapshotFreezeOutput>> ExecuteAsync(
        AgentRequest<SnapshotFreezeInput> request,
        AgentRunContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        var input = request.Input;
        if (input is null)
        {
            return AgentResult<SnapshotFreezeOutput>.Failure(
                "agent.input.required",
                "Snapshot freeze input is required.");
        }

        if (string.IsNullOrWhiteSpace(input.ProjectId.Value))
        {
            return AgentResult<SnapshotFreezeOutput>.Failure(
                "snapshot.project_id.required",
                "ProjectId is required.");
        }

        if (string.IsNullOrWhiteSpace(input.SnapshotId))
        {
            return AgentResult<SnapshotFreezeOutput>.Failure(
                "snapshot.snapshot_id.required",
                "SnapshotId is required.");
        }

        try
        {
            var manifest = await _freezeService.FreezeAsync(
                input.ProjectId,
                input.SnapshotId,
                input.CreatedAtUtc,
                input.Label,
                input.Exclusions,
                cancellationToken).ConfigureAwait(false);

            SnapshotExportArtifact? jsonExport = input.ExportJson ? SnapshotExportService.ExportJson(manifest) : null;
            SnapshotExportArtifact? csvExport = input.ExportCsv ? SnapshotExportService.ExportCsv(manifest) : null;

            var provenance = new List<string>
            {
                $"project:{manifest.ProjectId}",
                $"revision:{manifest.ProjectRevision}",
                $"snapshot:{manifest.SnapshotId}",
                $"method:{manifest.MethodVersion ?? ProjectSnapshotFreezer.DefaultMethodVersion}"
            };
            provenance.AddRange(manifest.SourceRefs.Where(r => !string.IsNullOrWhiteSpace(r)));

            var issues = manifest.Issues
                .Where(i => !string.IsNullOrWhiteSpace(i))
                .Distinct(StringComparer.Ordinal)
                .ToArray();

            var warnings = new List<string>();
            if (manifest.UnknownQuantities.Count > 0)
            {
                warnings.Add("Snapshot contains unknown quantities; unknown values are preserved as unknown, never zero.");
            }
            if (manifest.ProvisionalQuantities.Count > 0)
            {
                warnings.Add("Snapshot contains provisional quantities; provisional origin is preserved.");
            }
            if (manifest.Exclusions.Count > 0)
            {
                warnings.Add("Snapshot has explicit exclusions; excluded scope is not represented as zero quantity.");
            }

            var output = new SnapshotFreezeOutput(
                input.ProjectId,
                manifest.SnapshotId,
                manifest.ProjectRevision,
                manifest,
                jsonExport,
                csvExport);

            return AgentResult<SnapshotFreezeOutput>.Success(
                output,
                issues: issues.Length > 0 ? issues : null,
                warnings: warnings.Count > 0 ? warnings : null,
                provenanceRefs: provenance.Distinct(StringComparer.Ordinal).ToArray());
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or KeyNotFoundException)
        {
            return AgentResult<SnapshotFreezeOutput>.Failure(
                "snapshot.freeze.failed",
                $"Failed to freeze snapshot: {ex.Message}");
        }
    }
}
