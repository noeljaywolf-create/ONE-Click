using OneClick.Application.Commands;
using OneClick.Application.Contracts;
using OneClick.Application.Dispatch;
using OneClick.Application.Persistence;
using OneClick.Core.Ids;

namespace OneClick.Application.Agents.Takeoff;

/// <summary>
/// Input for deterministic local calibration metadata registration.
/// Wraps <see cref="SetCalibrationCommandHandler"/> behavior; no calibration math or geometry.
/// </summary>
/// <param name="ProjectId">Target project identity.</param>
/// <param name="CommandId">Stable command identity used for provenance.</param>
/// <param name="RegionId">Target document region identity.</param>
/// <param name="ControlPoints">Calibration control point metadata; validation is performed by the handler boundary.</param>
/// <param name="CalibrationNotes">Optional calibration notes metadata.</param>
public sealed record SetCalibrationInput(
    ProjectId ProjectId,
    CommandId CommandId,
    string RegionId,
    IReadOnlyList<CalibrationControlPoint> ControlPoints,
    string? CalibrationNotes = null);

/// <summary>
/// Typed output from deterministic calibration metadata registration.
/// </summary>
/// <param name="ProjectId">Target project identity.</param>
/// <param name="Revision">Project revision after successful save.</param>
/// <param name="CalibrationId">Calibration record ID for the region.</param>
/// <param name="RegionId">Target region ID.</param>
/// <param name="CreatedIds">IDs reported as created by the command handler.</param>
/// <param name="InvalidatedIds">IDs reported as invalidated by the command handler.</param>
/// <param name="IssueIds">Issue IDs reported by the command handler.</param>
public sealed record SetCalibrationOutput(
    ProjectId ProjectId,
    long Revision,
    string CalibrationId,
    string RegionId,
    IReadOnlyList<string> CreatedIds,
    IReadOnlyList<string> InvalidatedIds,
    IReadOnlyList<string> IssueIds);

/// <summary>
/// Deterministic local agent that registers calibration metadata for a document region.
/// It wraps existing <see cref="SetCalibrationCommandHandler"/> and in-memory domain state store behavior.
/// Metadata/refs only - no calibration math, no geometry, no network/AI calls.
/// </summary>
public sealed class SetCalibrationAgent : LocalAgentBase<SetCalibrationInput, SetCalibrationOutput>
{
    public const string AgentIdValue = "takeoff.calibration.set";
    private const string MethodVersion = "set-calibration.v1";

    public static readonly AgentDescriptor AgentDescriptor = new(
        new AgentId(AgentIdValue),
        AgentKind.Deterministic,
        "Set Calibration",
        "Registers calibration metadata for a document region in local project domain state.",
        "1.0.0");

    private readonly IProjectDomainStateStore _domainStateStore;
    private readonly SetCalibrationCommandHandler _handler;

    public SetCalibrationAgent(IProjectDomainStateStore domainStateStore)
        : base(AgentDescriptor)
    {
        _domainStateStore = domainStateStore ?? throw new ArgumentNullException(nameof(domainStateStore));
        _handler = new SetCalibrationCommandHandler(domainStateStore);
    }

    public override async Task<AgentResult<SetCalibrationOutput>> ExecuteAsync(
        AgentRequest<SetCalibrationInput> request,
        AgentRunContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        var input = request.Input;
        if (input is null)
        {
            return AgentResult<SetCalibrationOutput>.Failure(
                "agent.input.required",
                "Set calibration input is required.");
        }

        if (string.IsNullOrWhiteSpace(input.ProjectId.Value))
        {
            return AgentResult<SetCalibrationOutput>.Failure(
                "set-calibration.project_id.required",
                "ProjectId is required.");
        }

        if (string.IsNullOrWhiteSpace(input.CommandId.Value))
        {
            return AgentResult<SetCalibrationOutput>.Failure(
                "set-calibration.command_id.required",
                "CommandId is required for deterministic calibration provenance.");
        }

        if (string.IsNullOrWhiteSpace(input.RegionId))
        {
            return AgentResult<SetCalibrationOutput>.Failure(
                "set-calibration.region_id.required",
                "RegionId is required.");
        }

        if (input.ControlPoints is null || input.ControlPoints.Count < 2)
        {
            return AgentResult<SetCalibrationOutput>.Failure(
                "set-calibration.control_points.insufficient",
                "At least two control points are required for calibration metadata registration.",
                issues: ["issue:set-calibration:control-points-insufficient"]);
        }

        try
        {
            var state = await _domainStateStore.GetAsync(input.ProjectId, cancellationToken).ConfigureAwait(false);
            if (state is null)
            {
                return AgentResult<SetCalibrationOutput>.Failure(
                    "set-calibration.project_not_found",
                    $"Project domain state was not found for '{input.ProjectId.Value}'.",
                    issues: [$"issue:project-not-found:{input.ProjectId.Value}"]);
            }

            var command = new SetCalibrationCommand(input.RegionId, input.ControlPoints, input.CalibrationNotes);
            var handlerContext = new CommandHandlerContext(
                input.ProjectId,
                new ProjectRevision(state.Revision),
                input.CommandId,
                context.Actor);

            var handlerResult = await _handler.HandleAsync(command, handlerContext, cancellationToken).ConfigureAwait(false);
            var issueIds = Normalize(handlerResult.IssueIds);

            if (issueIds.Count > 0)
            {
                return AgentResult<SetCalibrationOutput>.Failure(
                    "set-calibration.rejected",
                    $"SetCalibration was rejected: {string.Join(", ", issueIds)}",
                    issues: issueIds,
                    provenanceRefs: BuildProvenance(input, state.Revision, MethodVersion, issueIds));
            }

            var saved = await _domainStateStore.GetAsync(input.ProjectId, cancellationToken).ConfigureAwait(false);
            var revision = saved?.Revision ?? state.Revision;
            var normalizedRegionId = input.RegionId.Trim();
            var calibrationId = $"calibration:{normalizedRegionId}";
            var createdIds = Normalize(handlerResult.CreatedIds);
            var invalidatedIds = Normalize(handlerResult.InvalidatedIds);

            var output = new SetCalibrationOutput(
                input.ProjectId,
                revision,
                calibrationId,
                normalizedRegionId,
                createdIds,
                invalidatedIds,
                issueIds);

            var provenanceRefs = new List<string>
            {
                $"project:{input.ProjectId.Value}",
                $"revision:{revision}",
                $"command:{input.CommandId.Value}",
                $"region:{normalizedRegionId}",
                $"calibration:{calibrationId}",
                $"method:{MethodVersion}"
            };
            provenanceRefs.AddRange(createdIds);
            provenanceRefs.AddRange(invalidatedIds);

            return AgentResult<SetCalibrationOutput>.Success(
                output,
                provenanceRefs: provenanceRefs.Distinct(StringComparer.Ordinal).ToArray());
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return AgentResult<SetCalibrationOutput>.Failure(
                "set-calibration.failed",
                $"Failed to register calibration metadata: {ex.Message}");
        }
    }

    private static IReadOnlyList<string> BuildProvenance(
        SetCalibrationInput input,
        long revision,
        string methodVersion,
        IReadOnlyList<string> refs)
    {
        var provenance = new List<string>
        {
            $"project:{input.ProjectId.Value}",
            $"revision:{revision}",
            $"command:{input.CommandId.Value}",
            $"region:{input.RegionId?.Trim() ?? string.Empty}",
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
