using OneClick.Core.TaggedValues;
using OneClick.Core.Units;

namespace OneClick.Core.Domain;

/// <summary>
/// Deterministic first-slice snapshot freezer. Freezes current project domain state into an
/// immutable SnapshotManifest with known/provisional/unknown quantities separated.
/// Route/cut calculations are delegated to <see cref="RouteLedgerCalculator"/> as the single source of truth.
/// Unknown is preserved as unknown (null value), never zero.
/// </summary>
public static class ProjectSnapshotFreezer
{
    public const string RouteLedger = RouteLedgerCalculator.RouteLedgerName;
    public const string CutLedger = RouteLedgerCalculator.CutLedgerName;
    public const string DefaultGroup = "synthetic-baseline";

    /// <summary>Snapshot manifest method version; per-quantity method versions come from RouteLedgerCalculator.</summary>
    public const string DefaultMethodVersion = "snapshot-freeze.v1";

    public static SnapshotManifest Freeze(
        ProjectDomainState state,
        string snapshotId,
        DateTimeOffset createdAtUtc,
        string? label = null,
        IReadOnlyList<string>? exclusions = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        return Freeze(state.ToSnapshot(), snapshotId, createdAtUtc, label, exclusions);
    }

    public static SnapshotManifest Freeze(
        ProjectDomainSnapshot snapshot,
        string snapshotId,
        DateTimeOffset createdAtUtc,
        string? label = null,
        IReadOnlyList<string>? exclusions = null)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        Guard.Required(snapshotId, nameof(snapshotId));

        var exclusionSet = NormalizeExclusions(exclusions);
        var snapshotSourceRefs = new List<string> { $"project:{snapshot.ProjectId.Value}", $"revision:{snapshot.Revision}" };
        var issues = new List<string>();

        var knownRoute = new List<(string PieceId, double RouteM, double CutM, IReadOnlyList<string> SourceRefs)>();
        var provisionalLines = new List<SnapshotQuantityLine>();
        var unknownLines = new List<SnapshotQuantityLine>();

        foreach (var piece in snapshot.Pieces.OrderBy(p => p.Id, StringComparer.Ordinal))
        {
            if (exclusionSet.Contains($"piece:{piece.Id}"))
                continue;

            CollectElevationIssues(piece.StartElevation, issues);
            CollectElevationIssues(piece.EndElevation, issues);

            var ledger = RouteLedgerCalculator.ComputePiece(piece);
            var refs = MergeRefs(snapshotSourceRefs, ledger.SourceRefs);

            var hasUnknownElevation = piece.StartElevation is UnknownValue || piece.EndElevation is UnknownValue;
            var hasProvisionalElevation = piece.StartElevation is ProvisionalValue || piece.EndElevation is ProvisionalValue;

            if (hasUnknownElevation || IsUnknownLedger(ledger))
            {
                var reason = GetUnknownReason(piece.StartElevation, piece.EndElevation)
                    ?? GetLedgerUnknownReason(ledger)
                    ?? "Unknown elevation affects quantity.";
                CollectLedgerIssues(ledger, issues);
                AddUnknownPieceLines(unknownLines, piece, ledger, refs, reason);
                continue;
            }

            if (hasProvisionalElevation)
            {
                AddProvisionalPieceLines(provisionalLines, piece, ledger, refs);
                continue;
            }

            if (ledger.RouteLength is KnownValue routeKnown && ledger.CutLength is KnownValue cutKnown)
            {
                knownRoute.Add((piece.Id, routeKnown.Value, cutKnown.Value, refs));
            }
            else
            {
                // Defensive: calculator should not produce non-known/non-unknown here, but never coerce to zero.
                var reason = GetLedgerUnknownReason(ledger) ?? "Route/cut could not be determined.";
                AddUnknownPieceLines(unknownLines, piece, ledger, refs, reason);
            }
        }

        var knownQuantities = new List<SnapshotQuantityLine>();
        if (knownRoute.Count > 0)
        {
            var allKnownRefs = MergeRefs(snapshotSourceRefs, knownRoute.SelectMany(p => p.SourceRefs).ToArray());
            knownQuantities.Add(new SnapshotQuantityLine(
                RouteLedger,
                DefaultGroup,
                SiUnit.Meter.ToString(),
                knownRoute.Sum(p => p.RouteM),
                SnapshotQuantityStatus.Known,
                sourceRefs: allKnownRefs,
                methodVersion: RouteLedgerCalculator.RouteMethodVersion));

            knownQuantities.Add(new SnapshotQuantityLine(
                CutLedger,
                DefaultGroup,
                SiUnit.Meter.ToString(),
                knownRoute.Sum(p => p.CutM),
                SnapshotQuantityStatus.Known,
                sourceRefs: allKnownRefs,
                methodVersion: RouteLedgerCalculator.CutMethodVersion));
        }

        var provisionalQuantities = provisionalLines
            .OrderBy(q => q.GroupKey, StringComparer.Ordinal)
            .ThenBy(q => q.Ledger, StringComparer.Ordinal)
            .ToList();
        var unknownQuantities = unknownLines
            .OrderBy(q => q.GroupKey, StringComparer.Ordinal)
            .ThenBy(q => q.Ledger, StringComparer.Ordinal)
            .ToList();

        // If no quantities at all, report explicit unknown totals rather than zero.
        if (knownQuantities.Count == 0 && provisionalQuantities.Count == 0 && unknownQuantities.Count == 0)
        {
            var reason = snapshot.Pieces.Count == 0 ? "No pieces in scope." : "All pieces excluded from scope.";
            unknownQuantities.Add(CreateUnknownTotalLine(RouteLedger, reason, snapshotSourceRefs, RouteLedgerCalculator.RouteMethodVersion));
            unknownQuantities.Add(CreateUnknownTotalLine(CutLedger, reason, snapshotSourceRefs, RouteLedgerCalculator.CutMethodVersion));
            issues.Add("issue:snapshot:no-in-scope-quantities");
        }

        if (unknownQuantities.Count > 0)
            issues.Add("issue:snapshot:partial-unknown");

        var distinctIssues = issues.Distinct(StringComparer.Ordinal).ToArray();

        return new SnapshotManifest(
            snapshotId: snapshotId,
            projectId: snapshot.ProjectId.Value,
            projectRevision: snapshot.Revision,
            createdAtUtc: createdAtUtc,
            label: label,
            sourceRefs: snapshotSourceRefs.Distinct(StringComparer.Ordinal).ToArray(),
            methodVersion: DefaultMethodVersion,
            knownQuantities: knownQuantities,
            provisionalQuantities: provisionalQuantities,
            unknownQuantities: unknownQuantities,
            exclusions: exclusionSet.OrderBy(x => x, StringComparer.Ordinal).ToArray(),
            issues: distinctIssues);
    }

    private static bool IsUnknownLedger(RoutePieceLedger ledger) =>
        ledger.RouteLength is UnknownValue || ledger.CutLength is UnknownValue;

    private static string? GetLedgerUnknownReason(RoutePieceLedger ledger)
    {
        if (ledger.RouteLength is UnknownValue r) return r.Reason;
        if (ledger.CutLength is UnknownValue c) return c.Reason;
        return null;
    }

    private static void AddProvisionalPieceLines(
        List<SnapshotQuantityLine> provisionalQuantities,
        RoutePiece piece,
        RoutePieceLedger ledger,
        IReadOnlyList<string> refs)
    {
        var issueRefs = CollectIssueRefs(piece.StartElevation, piece.EndElevation);

        if (ledger.RouteLength is KnownValue routeKnown)
        {
            provisionalQuantities.Add(new SnapshotQuantityLine(
                RouteLedger,
                piece.Id,
                SiUnit.Meter.ToString(),
                routeKnown.Value,
                SnapshotQuantityStatus.Provisional,
                reason: "Provisional elevation affects quantity.",
                sourceRefs: refs,
                methodVersion: ledger.RouteMethodVersion,
                issueRefs: issueRefs));
        }
        else
        {
            provisionalQuantities.Add(CreateUnknownOrProvisionalFallback(RouteLedger, piece, refs, ledger.RouteMethodVersion, issueRefs, isProvisional: true));
        }

        if (ledger.CutLength is KnownValue cutKnown)
        {
            provisionalQuantities.Add(new SnapshotQuantityLine(
                CutLedger,
                piece.Id,
                SiUnit.Meter.ToString(),
                cutKnown.Value,
                SnapshotQuantityStatus.Provisional,
                reason: "Provisional elevation affects quantity.",
                sourceRefs: refs,
                methodVersion: ledger.CutMethodVersion,
                issueRefs: issueRefs));
        }
        else
        {
            provisionalQuantities.Add(CreateUnknownOrProvisionalFallback(CutLedger, piece, refs, ledger.CutMethodVersion, issueRefs, isProvisional: true));
        }
    }

    private static SnapshotQuantityLine CreateUnknownOrProvisionalFallback(
        string ledger,
        RoutePiece piece,
        IReadOnlyList<string> refs,
        string methodVersion,
        IReadOnlyList<string> issueRefs,
        bool isProvisional)
    {
        // A provisional elevation that cannot be computed is treated as unknown, never zero.
        var reason = isProvisional
            ? "Provisional/unknown route or cut length."
            : "Unknown route or cut length.";
        var issues = issueRefs.Count > 0 ? issueRefs : [$"issue:quantity-unknown:{ledger}:{piece.Id}"];
        return new SnapshotQuantityLine(
            ledger,
            piece.Id,
            SiUnit.Meter.ToString(),
            value: null,
            status: SnapshotQuantityStatus.Unknown,
            reason: reason,
            sourceRefs: refs,
            methodVersion: methodVersion,
            issueRefs: issues);
    }

    private static void AddUnknownPieceLines(
        List<SnapshotQuantityLine> unknownQuantities,
        RoutePiece piece,
        RoutePieceLedger ledger,
        IReadOnlyList<string> refs,
        string reason)
    {
        var issueRefs = CollectIssueRefs(piece.StartElevation, piece.EndElevation);
        if (ledger.RouteLength is UnknownValue routeUnknown)
            issueRefs = issueRefs.Concat(routeUnknown.IssueRefs).Distinct(StringComparer.Ordinal).ToArray();
        if (ledger.CutLength is UnknownValue cutUnknown)
            issueRefs = issueRefs.Concat(cutUnknown.IssueRefs).Distinct(StringComparer.Ordinal).ToArray();
        if (issueRefs.Count == 0)
            issueRefs = [$"issue:quantity-unknown:{piece.Id}"];

        unknownQuantities.Add(new SnapshotQuantityLine(
            RouteLedger,
            piece.Id,
            SiUnit.Meter.ToString(),
            value: null,
            status: SnapshotQuantityStatus.Unknown,
            reason: reason,
            sourceRefs: refs,
            methodVersion: ledger.RouteMethodVersion,
            issueRefs: issueRefs));

        unknownQuantities.Add(new SnapshotQuantityLine(
            CutLedger,
            piece.Id,
            SiUnit.Meter.ToString(),
            value: null,
            status: SnapshotQuantityStatus.Unknown,
            reason: reason,
            sourceRefs: refs,
            methodVersion: ledger.CutMethodVersion,
            issueRefs: issueRefs));
    }

    private static SnapshotQuantityLine CreateUnknownTotalLine(
        string ledger,
        string reason,
        IReadOnlyList<string> sourceRefs,
        string methodVersion)
    {
        return new SnapshotQuantityLine(
            ledger,
            DefaultGroup,
            SiUnit.Meter.ToString(),
            value: null,
            status: SnapshotQuantityStatus.Unknown,
            reason: reason,
            sourceRefs: sourceRefs,
            methodVersion: methodVersion,
            issueRefs: ["issue:snapshot:no-in-scope-quantities"]);
    }

    private static HashSet<string> NormalizeExclusions(IReadOnlyList<string>? exclusions)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        if (exclusions is null) return set;
        foreach (var e in exclusions)
        {
            if (!string.IsNullOrWhiteSpace(e))
                set.Add(e);
        }
        return set;
    }

    private static void CollectElevationIssues(TaggedValue elevation, List<string> issues)
    {
        if (elevation is UnknownValue uv)
        {
            foreach (var i in uv.IssueRefs) issues.Add(i);
            issues.Add("issue:elevation:unknown");
        }
        else if (elevation is ProvisionalValue pv)
        {
            foreach (var i in pv.IssueRefs) issues.Add(i);
            issues.Add("issue:elevation:provisional");
        }
    }

    private static void CollectLedgerIssues(RoutePieceLedger ledger, List<string> issues)
    {
        if (ledger.RouteLength is UnknownValue ru)
            foreach (var i in ru.IssueRefs) issues.Add(i);
        if (ledger.CutLength is UnknownValue cu)
            foreach (var i in cu.IssueRefs) issues.Add(i);
    }

    private static IReadOnlyList<string> CollectIssueRefs(params TaggedValue[] values)
    {
        var refs = new List<string>();
        foreach (var v in values)
        {
            if (v is UnknownValue uv) refs.AddRange(uv.IssueRefs);
            if (v is ProvisionalValue pv) refs.AddRange(pv.IssueRefs);
        }
        return refs.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static string? GetUnknownReason(TaggedValue start, TaggedValue end)
    {
        if (start is UnknownValue us) return us.Reason;
        if (end is UnknownValue ue) return ue.Reason;
        return null;
    }

    private static IReadOnlyList<string> MergeRefs(IReadOnlyList<string> a, IReadOnlyList<string> b)
    {
        var merged = new List<string>(a.Count + b.Count);
        merged.AddRange(a);
        merged.AddRange(b);
        return merged.Distinct(StringComparer.Ordinal).ToArray();
    }
}

