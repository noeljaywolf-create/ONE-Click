using OneClick.Application.Persistence;
using OneClick.Core.Domain;
using OneClick.Core.Ids;

namespace OneClick.Application.Agents.Takeoff;

/// <summary>
/// Input for loading the synthetic baseline fixture into project domain state.
/// </summary>
/// <param name="ProjectId">Target project identity.</param>
/// <param name="FixturePath">Absolute or relative path to reference-network.json fixture.</param>
public sealed record BaselineLoadInput(
    ProjectId ProjectId,
    string FixturePath);

/// <summary>
/// Output from deterministic baseline load.
/// </summary>
public sealed record BaselineLoadOutput(
    ProjectId ProjectId,
    long Revision,
    int PieceCount,
    string FixturePath,
    IReadOnlyList<string> SourceRefs);

/// <summary>
/// Deterministic local agent that loads the synthetic reference-network baseline fixture into
/// project domain state. Uses <see cref="SyntheticReferenceNetworkLoader"/> and persists through
/// <see cref="IProjectDomainStateStore"/>. No network/AI calls.
/// </summary>
public sealed class BaselineLoadAgent : LocalAgentBase<BaselineLoadInput, BaselineLoadOutput>
{
    public static readonly AgentDescriptor AgentDescriptor = new(
        new AgentId("takeoff.baseline.load"),
        AgentKind.Deterministic,
        "Baseline Load",
        "Loads the synthetic reference-network baseline fixture into project domain state.",
        "1.0.0");

    private readonly IProjectDomainStateStore _domainStateStore;

    public BaselineLoadAgent(IProjectDomainStateStore domainStateStore)
        : base(AgentDescriptor)
    {
        _domainStateStore = domainStateStore ?? throw new ArgumentNullException(nameof(domainStateStore));
    }

    public override async Task<AgentResult<BaselineLoadOutput>> ExecuteAsync(
        AgentRequest<BaselineLoadInput> request,
        AgentRunContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        var input = request.Input;
        if (input is null)
        {
            return AgentResult<BaselineLoadOutput>.Failure(
                "agent.input.required",
                "Baseline load input is required.");
        }

        if (string.IsNullOrWhiteSpace(input.ProjectId.Value))
        {
            return AgentResult<BaselineLoadOutput>.Failure(
                "baseline.project_id.required",
                "ProjectId is required.");
        }

        if (string.IsNullOrWhiteSpace(input.FixturePath))
        {
            return AgentResult<BaselineLoadOutput>.Failure(
                "baseline.fixture_path.required",
                "FixturePath is required.");
        }

        if (!File.Exists(input.FixturePath))
        {
            return AgentResult<BaselineLoadOutput>.Failure(
                "baseline.fixture.not_found",
                $"Baseline fixture was not found: '{input.FixturePath}'.");
        }

        try
        {
            var existing = await _domainStateStore.GetSnapshotAsync(input.ProjectId, cancellationToken).ConfigureAwait(false);
            var baseline = SyntheticReferenceNetworkLoader.LoadBaselineFromFile(input.FixturePath, input.ProjectId);
            await _domainStateStore.SaveAsync(baseline, cancellationToken).ConfigureAwait(false);

            var sourceRefs = baseline.Pieces
                .SelectMany(p => p.SourceRefs)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(r => r, StringComparer.Ordinal)
                .ToArray();

            var provenance = new List<string>
            {
                $"project:{input.ProjectId.Value}",
                $"revision:{baseline.Revision}",
                $"fixture:{Path.GetFullPath(input.FixturePath)}",
                $"method:{SyntheticReferenceNetworkLoader.DefaultMethodVersion}"
            };
            provenance.AddRange(sourceRefs);

            var output = new BaselineLoadOutput(
                input.ProjectId,
                baseline.Revision,
                baseline.Pieces.Count,
                Path.GetFullPath(input.FixturePath),
                sourceRefs);

            var warnings = new List<string>();
            if (existing is not null)
            {
                warnings.Add(
                    $"Existing project domain state for '{input.ProjectId.Value}' (revision {existing.Revision}) was replaced by synthetic baseline load.");
            }

            return AgentResult<BaselineLoadOutput>.Success(
                output,
                warnings: warnings.Count > 0 ? warnings : null,
                provenanceRefs: provenance.Distinct(StringComparer.Ordinal).ToArray());
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or FormatException or System.Text.Json.JsonException or IOException or UnauthorizedAccessException)
        {
            return AgentResult<BaselineLoadOutput>.Failure(
                "baseline.load.failed",
                $"Failed to load synthetic baseline: {ex.Message}");
        }
    }
}
