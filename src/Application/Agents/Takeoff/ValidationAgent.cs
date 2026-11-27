using OneClick.Application.Persistence;
using OneClick.Core.Domain;
using OneClick.Core.Ids;
using OneClick.Core.TaggedValues;

namespace OneClick.Application.Agents.Takeoff;

/// <summary>
/// Input for deterministic local takeoff validation checks.
/// </summary>
/// <param name="ProjectId">Target project identity.</param>
/// <param name="Scope">Optional scope filter. When provided, only pieces whose Id or Group contains the scope value (ordinal, case-insensitive) are checked.</param>
public sealed record ValidationInput(ProjectId ProjectId, string? Scope = null);

/// <summary>
/// Severity of a deterministic validation finding. This is a local check classification, not an engineering certification.
/// </summary>
public static class ValidationFindingSeverity
{
    public const string Issue = "Issue";
    public const string Warning = "Warning";
    public const string Info = "Info";
}

/// <summary>
/// A single deterministic validation finding with inspectable scope and provenance refs.
/// </summary>
public sealed record ValidationFinding(
    string Severity,
    string Code,
    string Message,
    string? Scope = null,
    IReadOnlyList<string>? SourceRefs = null)
{
    public IReadOnlyList<string> SourceRefs { get; } = Normalize(SourceRefs);

    private static IReadOnlyList<string> Normalize(IReadOnlyList<string>? values)
    {
        if (values is null || values.Count == 0)
            return Array.Empty<string>();

        var copy = new string[values.Count];
        for (var i = 0; i < values.Count; i++)
            copy[i] = values[i] ?? throw new ArgumentException("Source ref is required.", nameof(values));
        return Array.AsReadOnly(copy);
    }
}

/// <summary>
/// Counts summarizing deterministic validation check results.
/// </summary>
public sealed record ValidationCounts(
    int PiecesInProject,
    int PiecesChecked,
    int FindingsTotal,
    int IssueCount,
    int WarningCount,
    int InfoCount,
    int KnownQuantityCount,
    int ProvisionalQuantityCount,
    int UnknownQuantityCount);

/// <summary>
/// Typed deterministic validation summary. Findings are local check results only; they are not
/// NFPA/regulatory certification, manufacturer approval, or a release decision.
/// </summary>
public sealed record ValidationOutput(
    ProjectId ProjectId,
    long ProjectRevision,
    string Scope,
    ValidationCounts Counts,
    IReadOnlyList<ValidationFinding> Findings,
    IReadOnlyList<string> Coverage,
    IReadOnlyList<string> Limitations,
    IReadOnlyList<string> SourceRefs);

/// <summary>
/// Deterministic local agent that runs bounded takeoff validation checks over project domain state.
/// Uses existing deterministic rules/calculators (e.g. <see cref="RouteLedgerCalculator"/>) and reports
/// issues/warnings/provenance refs. It does not invent NFPA rules, manufacturer data, or engineering
/// pass/fail certification claims. Limited coverage is reported explicitly.
/// </summary>
public sealed class ValidationAgent : LocalAgentBase<ValidationInput, ValidationOutput>
{
    public const string AgentIdValue = "takeoff.validation.run";

    public static readonly AgentDescriptor AgentDescriptor = new(
        new AgentId(AgentIdValue),
        AgentKind.Deterministic,
        "Takeoff Validation",
        "Runs deterministic local takeoff checks and reports inspectable issues, warnings, coverage, and provenance. Not NFPA/certification/release approval.",
        "1.0.0");

    private readonly IProjectDomainStateStore _domainStateStore;

    public ValidationAgent(IProjectDomainStateStore domainStateStore)
        : base(AgentDescriptor)
    {
        _domainStateStore = domainStateStore ?? throw new ArgumentNullException(nameof(domainStateStore));
    }

    public override async Task<AgentResult<ValidationOutput>> ExecuteAsync(
        AgentRequest<ValidationInput> request,
        AgentRunContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        var input = request.Input;
        if (input is null)
        {
            return AgentResult<ValidationOutput>.Failure(
                "agent.input.required",
                "Validation input is required.");
        }

        if (string.IsNullOrWhiteSpace(input.ProjectId.Value))
        {
            return AgentResult<ValidationOutput>.Failure(
                "validation.project_id.required",
                "ProjectId is required.");
        }

        var snapshot = await _domainStateStore.GetSnapshotAsync(input.ProjectId, cancellationToken).ConfigureAwait(false);
        if (snapshot is null)
        {
            return AgentResult<ValidationOutput>.Failure(
                "validation.project.not_found",
                $"Project '{input.ProjectId.Value}' was not found in domain state store.");
        }

        var scope = string.IsNullOrWhiteSpace(input.Scope) ? "all" : input.Scope.Trim();
        var pieces = SelectPieces(snapshot.Pieces, input.Scope);
        var findings = new List<ValidationFinding>();
        var provenance = new List<string>
        {
            $"project:{snapshot.ProjectId.Value}",
            $"revision:{snapshot.Revision}",
            $"scope:{scope}",
            $"method:takeoff-validation.v1"
        };

        AddProjectStateChecks(snapshot, scope, pieces, findings, provenance);
        AddPieceChecks(pieces, findings, provenance);
        AddLedgerChecks(pieces, findings, provenance);
        AddSnapshotManifestChecks(snapshot, findings, provenance);

        var orderedFindings = findings
            .OrderBy(f => SeverityRank(f.Severity))
            .ThenBy(f => f.Code, StringComparer.Ordinal)
            .ThenBy(f => f.Scope ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(f => f.Message, StringComparer.Ordinal)
            .ToArray();

        var issueCount = orderedFindings.Count(f => string.Equals(f.Severity, ValidationFindingSeverity.Issue, StringComparison.Ordinal));
        var warningCount = orderedFindings.Count(f => string.Equals(f.Severity, ValidationFindingSeverity.Warning, StringComparison.Ordinal));
        var infoCount = orderedFindings.Count(f => string.Equals(f.Severity, ValidationFindingSeverity.Info, StringComparison.Ordinal));

        var (knownQty, provisionalQty, unknownQty) = CountQuantityStates(pieces);

        var coverage = BuildCoverage();
        var limitations = BuildLimitations(scope, pieces.Count, snapshot.Pieces.Count);

        var output = new ValidationOutput(
            input.ProjectId,
            snapshot.Revision,
            scope,
            new ValidationCounts(
                snapshot.Pieces.Count,
                pieces.Count,
                orderedFindings.Length,
                issueCount,
                warningCount,
                infoCount,
                knownQty,
                provisionalQty,
                unknownQty),
            orderedFindings,
            coverage,
            limitations,
            provenance.Distinct(StringComparer.Ordinal).OrderBy(r => r, StringComparer.Ordinal).ToArray());

        var resultIssues = orderedFindings
            .Where(f => string.Equals(f.Severity, ValidationFindingSeverity.Issue, StringComparison.Ordinal))
            .Select(f => f.Code)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var resultWarnings = orderedFindings
            .Where(f => string.Equals(f.Severity, ValidationFindingSeverity.Warning, StringComparison.Ordinal))
            .Select(f => f.Code)
            .Concat(limitations.Select(l => $"limitation:{l}"))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        return AgentResult<ValidationOutput>.Success(
            output,
            issues: resultIssues.Length > 0 ? resultIssues : null,
            warnings: resultWarnings.Length > 0 ? resultWarnings : null,
            provenanceRefs: output.SourceRefs);
    }

    private static IReadOnlyList<RoutePiece> SelectPieces(IReadOnlyList<RoutePiece> pieces, string? scope)
    {
        var ordered = pieces.OrderBy(p => p.Id, StringComparer.Ordinal).ToArray();
        if (string.IsNullOrWhiteSpace(scope))
            return ordered;

        var filter = scope.Trim();
        return ordered
            .Where(p => p.Id.Contains(filter, StringComparison.OrdinalIgnoreCase)
                || p.Group.Contains(filter, StringComparison.OrdinalIgnoreCase))
            .ToArray();
    }

    private static void AddProjectStateChecks(
        ProjectDomainSnapshot snapshot,
        string scope,
        IReadOnlyList<RoutePiece> checkedPieces,
        List<ValidationFinding> findings,
        List<string> provenance)
    {
        if (snapshot.Pieces.Count == 0)
        {
            findings.Add(new ValidationFinding(
                ValidationFindingSeverity.Warning,
                "validation.project.no_pieces",
                "Project domain state contains no route pieces. Validation coverage is limited to project-level state.",
                "project",
                [$"project:{snapshot.ProjectId.Value}", $"revision:{snapshot.Revision}"]));
        }

        if (!string.Equals(scope, "all", StringComparison.OrdinalIgnoreCase) && checkedPieces.Count == 0)
        {
            findings.Add(new ValidationFinding(
                ValidationFindingSeverity.Warning,
                "validation.scope.no_matches",
                $"Scope '{scope}' matched no route pieces. No piece/ledger checks were run for this scope.",
                scope,
                [$"project:{snapshot.ProjectId.Value}", $"revision:{snapshot.Revision}"]));
        }
    }

    private static void AddPieceChecks(
        IReadOnlyList<RoutePiece> pieces,
        List<ValidationFinding> findings,
        List<string> provenance)
    {
        foreach (var piece in pieces)
        {
            var scope = $"piece:{piece.Id}";
            AddRefs(provenance, piece.SourceRefs);
            provenance.Add(scope);

            if (piece.SourceRefs.Count == 0)
            {
                findings.Add(new ValidationFinding(
                    ValidationFindingSeverity.Warning,
                    "validation.piece.source_refs.missing",
                    $"Piece '{piece.Id}' has no source refs; provenance is incomplete.",
                    scope));
            }

            if (string.IsNullOrWhiteSpace(piece.MethodVersion))
            {
                findings.Add(new ValidationFinding(
                    ValidationFindingSeverity.Warning,
                    "validation.piece.method_version.missing",
                    $"Piece '{piece.Id}' has no method version; provenance is incomplete.",
                    scope,
                    piece.SourceRefs));
            }

            CheckElevation(piece.Id, "start", piece.StartElevation, findings, piece.SourceRefs);
            CheckElevation(piece.Id, "end", piece.EndElevation, findings, piece.SourceRefs);

            if (piece.StartNetOffsetM < 0d || piece.EndNetOffsetM < 0d)
            {
                findings.Add(new ValidationFinding(
                    ValidationFindingSeverity.Issue,
                    "validation.piece.net_offset.negative",
                    $"Piece '{piece.Id}' has a negative net offset; route/cut ledger inputs are invalid.",
                    scope,
                    piece.SourceRefs));
            }

            var routeM = DistanceMeters(piece);
            var cutM = routeM - piece.StartNetOffsetM - piece.EndNetOffsetM;
            if (double.IsFinite(cutM) && cutM < -1e-9)
            {
                findings.Add(new ValidationFinding(
                    ValidationFindingSeverity.Warning,
                    "validation.piece.cut_length.negative",
                    $"Piece '{piece.Id}' has synthetic cut length below zero ({cutM:0.###} m) based on declared net offsets. This is a deterministic arithmetic check, not a manufacturer takeout claim.",
                    scope,
                    piece.SourceRefs));
            }
        }
    }

    private static void CheckElevation(
        string pieceId,
        string endName,
        TaggedValue elevation,
        List<ValidationFinding> findings,
        IReadOnlyList<string> sourceRefs)
    {
        var scope = $"piece:{pieceId}:{endName}-elevation";
        switch (elevation)
        {
            case UnknownValue unknown:
                findings.Add(new ValidationFinding(
                    ValidationFindingSeverity.Issue,
                    "validation.elevation.unknown",
                    $"Piece '{pieceId}' has unknown {endName} elevation: {unknown.Reason}",
                    scope,
                    Merge(sourceRefs, unknown.SourceRefs, unknown.IssueRefs)));
                break;
            case ProvisionalValue provisional:
                findings.Add(new ValidationFinding(
                    ValidationFindingSeverity.Warning,
                    "validation.elevation.provisional",
                    $"Piece '{pieceId}' has provisional {endName} elevation (value {provisional.Value:0.###} {provisional.Unit}). Provisional origin is preserved.",
                    scope,
                    Merge(sourceRefs, provisional.SourceRefs, provisional.IssueRefs, provisional.AssumptionRefs)));
                break;
            case KnownValue known:
                if (!double.IsFinite(known.Value))
                {
                    findings.Add(new ValidationFinding(
                        ValidationFindingSeverity.Issue,
                        "validation.elevation.non_finite",
                        $"Piece '{pieceId}' has non-finite known {endName} elevation.",
                        scope,
                        Merge(sourceRefs, known.SourceRefs)));
                }
                break;
        }
    }

    private static void AddLedgerChecks(
        IReadOnlyList<RoutePiece> pieces,
        List<ValidationFinding> findings,
        List<string> provenance)
    {
        if (pieces.Count == 0)
            return;

        var ledgers = RouteLedgerCalculator.Compute(pieces);
        provenance.Add($"route-method:{RouteLedgerCalculator.RouteMethodId}@{RouteLedgerCalculator.RouteMethodVersion}");
        provenance.Add($"cut-method:{RouteLedgerCalculator.CutMethodId}@{RouteLedgerCalculator.CutMethodVersion}");
        provenance.Add($"ledger-method:{RouteLedgerCalculator.MethodVersion}");

        foreach (var issueId in ledgers.IssueIds.Where(i => !string.IsNullOrWhiteSpace(i)).Distinct(StringComparer.Ordinal))
        {
            findings.Add(new ValidationFinding(
                ValidationFindingSeverity.Warning,
                "validation.ledger.issue",
                $"Route/cut ledger reported issue '{issueId}'.",
                "ledger",
                [issueId]));
            provenance.Add(issueId);
        }

        foreach (var pieceLedger in ledgers.Pieces)
        {
            AddRefs(provenance, pieceLedger.SourceRefs);
            ReportLedgerUnknown(pieceLedger.PieceId, RouteLedgerCalculator.RouteLedgerName, pieceLedger.RouteLength, findings, pieceLedger.SourceRefs);
            ReportLedgerUnknown(pieceLedger.PieceId, RouteLedgerCalculator.CutLedgerName, pieceLedger.CutLength, findings, pieceLedger.SourceRefs);
        }

        ReportTotalUnknown(RouteLedgerCalculator.RouteLedgerName, ledgers.RouteTotal, findings);
        ReportTotalUnknown(RouteLedgerCalculator.CutLedgerName, ledgers.CutTotal, findings);
    }

    private static void ReportLedgerUnknown(
        string pieceId,
        string ledgerName,
        TaggedValue value,
        List<ValidationFinding> findings,
        IReadOnlyList<string> sourceRefs)
    {
        if (value is not UnknownValue unknown)
            return;

        findings.Add(new ValidationFinding(
            ValidationFindingSeverity.Issue,
            "validation.ledger.quantity_unknown",
            $"Piece '{pieceId}' has unknown {ledgerName} quantity: {unknown.Reason}",
            $"piece:{pieceId}:{ledgerName}",
            Merge(sourceRefs, unknown.SourceRefs, unknown.IssueRefs)));
    }

    private static void ReportTotalUnknown(string ledgerName, TaggedValue total, List<ValidationFinding> findings)
    {
        if (total is not UnknownValue unknown)
            return;

        findings.Add(new ValidationFinding(
            ValidationFindingSeverity.Warning,
            "validation.ledger.total_unknown",
            $"{ledgerName} total is unknown: {unknown.Reason}",
            $"ledger:{ledgerName}:total",
            Merge(unknown.SourceRefs, unknown.IssueRefs)));
    }

    private static void AddSnapshotManifestChecks(
        ProjectDomainSnapshot snapshot,
        List<ValidationFinding> findings,
        List<string> provenance)
    {
        foreach (var manifest in snapshot.SnapshotManifests.OrderBy(m => m.SnapshotId, StringComparer.Ordinal))
        {
            var scope = $"snapshot:{manifest.SnapshotId}";
            provenance.Add(scope);
            AddRefs(provenance, manifest.SourceRefs);

            if (manifest.ProjectRevision < 0 || manifest.ProjectRevision > snapshot.Revision)
            {
                findings.Add(new ValidationFinding(
                    ValidationFindingSeverity.Warning,
                    "validation.snapshot.revision.out_of_range",
                    $"Snapshot '{manifest.SnapshotId}' has project revision {manifest.ProjectRevision} outside current project revision range 0..{snapshot.Revision}.",
                    scope,
                    manifest.SourceRefs));
            }

            foreach (var issue in manifest.Issues.Where(i => !string.IsNullOrWhiteSpace(i)).Distinct(StringComparer.Ordinal))
            {
                findings.Add(new ValidationFinding(
                    ValidationFindingSeverity.Info,
                    "validation.snapshot.issue_recorded",
                    $"Snapshot '{manifest.SnapshotId}' records issue '{issue}'.",
                    scope,
                    Merge(manifest.SourceRefs, [issue])));
            }

            if (manifest.UnknownQuantities.Count > 0)
            {
                findings.Add(new ValidationFinding(
                    ValidationFindingSeverity.Info,
                    "validation.snapshot.has_unknown_quantities",
                    $"Snapshot '{manifest.SnapshotId}' contains {manifest.UnknownQuantities.Count} unknown quantity line(s); unknown is preserved as unknown, not zero.",
                    scope,
                    manifest.SourceRefs));
            }
        }
    }

    private static (int Known, int Provisional, int Unknown) CountQuantityStates(IReadOnlyList<RoutePiece> pieces)
    {
        if (pieces.Count == 0)
            return (0, 0, 0);

        var ledgers = RouteLedgerCalculator.Compute(pieces);
        var known = 0;
        var provisional = 0;
        var unknown = 0;
        foreach (var piece in ledgers.Pieces)
        {
            // Route and cut are counted as quantity lines for inspectable summary.
            CountValue(piece.RouteLength, ref known, ref provisional, ref unknown);
            CountValue(piece.CutLength, ref known, ref provisional, ref unknown);
        }

        return (known, provisional, unknown);
    }

    private static void CountValue(TaggedValue value, ref int known, ref int provisional, ref int unknown)
    {
        switch (value)
        {
            case KnownValue:
                known++;
                break;
            case ProvisionalValue:
                provisional++;
                break;
            case UnknownValue:
                unknown++;
                break;
        }
    }

    private static IReadOnlyList<string> BuildCoverage()
    {
        return new[]
        {
            "Project domain state presence and basic piece/scope checks.",
            "Route piece elevation tagged-value checks (known/provisional/unknown/finite) using existing domain value types.",
            "Deterministic route/cut ledger checks via RouteLedgerCalculator (G01 route, synthetic-net-offset cut).",
            "Snapshot manifest metadata/issue presence checks when manifests exist in domain state."
        };
    }

    private static IReadOnlyList<string> BuildLimitations(string scope, int checkedPieces, int totalPieces)
    {
        var limitations = new List<string>
        {
            "This agent performs bounded deterministic local checks only; it is not NFPA/regulatory certification, engineering approval, manufacturer validation, or a release decision.",
            "No cloud LLM/network calls are used. No manufacturer catalog, fitting takeout, or NFPA rule engine is included in this check.",
            "Coverage is limited to available domain state and deterministic route/cut ledger rules; unsupported or absent data is reported as unknown/limited, not pass."
        };

        if (!string.Equals(scope, "all", StringComparison.OrdinalIgnoreCase))
        {
            limitations.Add($"Scope filter '{scope}' was applied; only {checkedPieces} of {totalPieces} project piece(s) were checked.");
        }

        return limitations;
    }

    private static double DistanceMeters(RoutePiece piece)
    {
        var dx = piece.EndM.X - piece.StartM.X;
        var dy = piece.EndM.Y - piece.StartM.Y;
        var dz = piece.EndM.Z - piece.StartM.Z;
        return Math.Sqrt((dx * dx) + (dy * dy) + (dz * dz));
    }

    private static void AddRefs(List<string> target, IReadOnlyList<string> refs)
    {
        foreach (var r in refs)
        {
            if (!string.IsNullOrWhiteSpace(r))
                target.Add(r);
        }
    }

    private static IReadOnlyList<string> Merge(params IReadOnlyList<string>[] refs)
    {
        var list = new List<string>();
        foreach (var group in refs)
        {
            foreach (var r in group)
            {
                if (!string.IsNullOrWhiteSpace(r))
                    list.Add(r);
            }
        }

        return list.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static int SeverityRank(string severity)
    {
        if (string.Equals(severity, ValidationFindingSeverity.Issue, StringComparison.Ordinal)) return 0;
        if (string.Equals(severity, ValidationFindingSeverity.Warning, StringComparison.Ordinal)) return 1;
        if (string.Equals(severity, ValidationFindingSeverity.Info, StringComparison.Ordinal)) return 2;
        return 3;
    }
}
