using OneClick.Application.Persistence;
using OneClick.Core.Domain;
using OneClick.Core.Ids;

namespace OneClick.Application.Agents.Takeoff;

/// <summary>
/// Input for deterministic route/cut ledger calculation.
/// </summary>
/// <param name="ProjectId">Target project identity.</param>
public sealed record LedgerCalculationInput(ProjectId ProjectId);

/// <summary>
/// Typed output from deterministic route/cut ledger calculation.
/// </summary>
public sealed record LedgerCalculationOutput(
    ProjectId ProjectId,
    long ProjectRevision,
    int PieceCount,
    ProjectRouteLedgers Ledgers);

/// <summary>
/// Deterministic local agent that computes route/cut ledgers for current project domain state.
/// Uses <see cref="RouteLedgerCalculator"/> as the single source of truth. No network/AI calls.
/// </summary>
public sealed class LedgerCalculationAgent : LocalAgentBase<LedgerCalculationInput, LedgerCalculationOutput>
{
    public static readonly AgentDescriptor AgentDescriptor = new(
        new AgentId("takeoff.ledger.calculate"),
        AgentKind.Deterministic,
        "Ledger Calculation",
        "Computes deterministic route/cut ledgers for project domain state with provenance and issues.",
        "1.0.0");

    private readonly IProjectDomainStateStore _domainStateStore;

    public LedgerCalculationAgent(IProjectDomainStateStore domainStateStore)
        : base(AgentDescriptor)
    {
        _domainStateStore = domainStateStore ?? throw new ArgumentNullException(nameof(domainStateStore));
    }

    public override async Task<AgentResult<LedgerCalculationOutput>> ExecuteAsync(
        AgentRequest<LedgerCalculationInput> request,
        AgentRunContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        var input = request.Input;
        if (input is null)
        {
            return AgentResult<LedgerCalculationOutput>.Failure(
                "agent.input.required",
                "Ledger calculation input is required.");
        }

        if (string.IsNullOrWhiteSpace(input.ProjectId.Value))
        {
            return AgentResult<LedgerCalculationOutput>.Failure(
                "ledger.project_id.required",
                "ProjectId is required.");
        }

        var snapshot = await _domainStateStore.GetSnapshotAsync(input.ProjectId, cancellationToken).ConfigureAwait(false);
        if (snapshot is null)
        {
            return AgentResult<LedgerCalculationOutput>.Failure(
                "ledger.project.not_found",
                $"Project '{input.ProjectId.Value}' was not found in domain state store.");
        }

        var orderedPieces = snapshot.Pieces.OrderBy(p => p.Id, StringComparer.Ordinal).ToArray();
        var ledgers = RouteLedgerCalculator.Compute(orderedPieces);

        var provenance = new List<string>
        {
            $"project:{snapshot.ProjectId.Value}",
            $"revision:{snapshot.Revision}",
            $"method:{RouteLedgerCalculator.MethodVersion}",
            $"route-method:{RouteLedgerCalculator.RouteMethodId}@{RouteLedgerCalculator.RouteMethodVersion}",
            $"cut-method:{RouteLedgerCalculator.CutMethodId}@{RouteLedgerCalculator.CutMethodVersion}"
        };

        foreach (var piece in ledgers.Pieces)
        {
            foreach (var sourceRef in piece.SourceRefs)
            {
                if (!string.IsNullOrWhiteSpace(sourceRef))
                    provenance.Add(sourceRef);
            }
        }

        var issues = ledgers.IssueIds
            .Where(i => !string.IsNullOrWhiteSpace(i))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var warnings = new List<string>();
        if (snapshot.Pieces.Count == 0)
        {
            warnings.Add("Project domain state contains no route pieces; ledger totals are empty/zero-known by calculator semantics.");
        }

        var output = new LedgerCalculationOutput(
            input.ProjectId,
            snapshot.Revision,
            snapshot.Pieces.Count,
            ledgers);

        return AgentResult<LedgerCalculationOutput>.Success(
            output,
            issues: issues.Length > 0 ? issues : null,
            warnings: warnings.Count > 0 ? warnings : null,
            provenanceRefs: provenance.Distinct(StringComparer.Ordinal).ToArray());
    }
}
