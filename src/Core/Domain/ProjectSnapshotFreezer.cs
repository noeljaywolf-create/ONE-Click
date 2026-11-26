using OneClick.Core.Formulas;
using OneClick.Core.Geometry;
using OneClick.Core.TaggedValues;
using OneClick.Core.Units;

namespace OneClick.Core.Domain;

/// <summary>
/// Deterministic first-slice snapshot freezer. Freezes current project domain state into an
/// immutable SnapshotManifest with known/provisional/unknown quantities separated.
/// Unknown is preserved as unknown (null value), never zero.
/// </summary>
public static class ProjectSnapshotFreezer
{
    public const string RouteLedger = "route";
    public const string CutLedger = "synthetic-cut";
    public const string DefaultGroup = "synthetic-baseline";
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
        var sourceRefs = new List<string> { $"project:{snapshot.ProjectId.Value}", $"revision:{snapshot.Revision}" };
        var issues = new List<string>();

        var knownRoute = new List<(RoutePiece Piece, double RouteM, double CutM)>();
        var unknownRoutePieces = new List<RoutePiece>();
        var provisionalRoutePieces = new List<RoutePiece>();

        foreach (var piece in snapshot.Pieces.OrderBy(p => p.Id, StringComparer.Ordinal))
        {
            if (exclusionSet.Contains($"piece:{piece.Id}"))
            {
                continue;
            }

            var hasUnknownElevation = piece.StartElevation is UnknownValue || piece.EndElevation is UnknownValue;
            var hasProvisionalElevation = piece.StartElevation is ProvisionalValue || piece.EndElevation is ProvisionalValue;

            CollectElevationIssues(piece.StartElevation, issues);
            CollectElevationIssues(piece.EndElevation, issues);

            if (hasUnknownElevation)
            {
                unknownRoutePieces.Add(piece);
                continue;
            }

            if (hasProvisionalElevation)
            {
                provisionalRoutePieces.Add(piece);
                // Provisional elevation means length is provisional in this slice.
                // Compute geometric length but mark as provisional.
                continue;
            }

            var routeResult = GeometryFormulas.StraightSegmentLength(
                SpatialEndpoint.FromPoint3D(piece.StartM),
                SpatialEndpoint.FromPoint3D(piece.EndM),
                piece.SourceRefs);
            if (routeResult is KnownValue knownRouteValue)
            {
                var cutM = Guard.Finite(knownRouteValue.Value - piece.StartNetOffsetM - piece.EndNetOffsetM, "cutLengthM");
                knownRoute.Add((piece, knownRouteValue.Value, cutM));
            }
            else if (routeResult is UnknownValue uv)
            {
                unknownRoutePieces.Add(piece);
                issues.AddRange(uv.IssueRefs);
            }
            else if (routeResult is ProvisionalValue pv)
            {
                provisionalRoutePieces.Add(piece);
            }
        }

        var knownQuantities = new List<SnapshotQuantityLine>();
        var provisionalQuantities = new List<SnapshotQuantityLine>();
        var unknownQuantities = new List<SnapshotQuantityLine>();

        if (knownRoute.Count > 0)
        {
            knownQuantities.Add(new SnapshotQuantityLine(
                RouteLedger,
                DefaultGroup,
                SiUnit.Meter.ToString(),
                knownRoute.Sum(p => p.RouteM),
                SnapshotQuantityStatus.Known,
                sourceRefs: sourceRefs,
                methodVersion: DefaultMethodVersion));

            knownQuantities.Add(new SnapshotQuantityLine(
                CutLedger,
                DefaultGroup,
                SiUnit.Meter.ToString(),
                knownRoute.Sum(p => p.CutM),
                SnapshotQuantityStatus.Known,
                sourceRefs: sourceRefs,
                methodVersion: DefaultMethodVersion));
        }

        foreach (var piece in provisionalRoutePieces.OrderBy(p => p.Id, StringComparer.Ordinal))
        {
            var routeResult = GeometryFormulas.StraightSegmentLength(
                SpatialEndpoint.FromPoint3D(piece.StartM),
                SpatialEndpoint.FromPoint3D(piece.EndM),
                piece.SourceRefs);
            var routeM = routeResult switch
            {
                KnownValue k => k.Value,
                ProvisionalValue p => p.Value,
                _ => (double?)null
            };
            if (routeM.HasValue)
            {
                var cutM = Guard.Finite(routeM.Value - piece.StartNetOffsetM - piece.EndNetOffsetM, "cutLengthM");
                var refs = MergeRefs(sourceRefs, piece.SourceRefs);
                provisionalQuantities.Add(new SnapshotQuantityLine(RouteLedger, piece.Id, SiUnit.Meter.ToString(), routeM.Value, SnapshotQuantityStatus.Provisional,
                    reason: "Provisional elevation affects quantity.", sourceRefs: refs, methodVersion: DefaultMethodVersion,
                    issueRefs: CollectIssueRefs(piece.StartElevation, piece.EndElevation)));
                provisionalQuantities.Add(new SnapshotQuantityLine(CutLedger, piece.Id, SiUnit.Meter.ToString(), cutM, SnapshotQuantityStatus.Provisional,
                    reason: "Provisional elevation affects quantity.", sourceRefs: refs, methodVersion: DefaultMethodVersion,
                    issueRefs: CollectIssueRefs(piece.StartElevation, piece.EndElevation)));
            }
            else
            {
                unknownQuantities.Add(CreateUnknownPieceLine(RouteLedger, piece, sourceRefs, "Provisional/unknown route length."));
                unknownQuantities.Add(CreateUnknownPieceLine(CutLedger, piece, sourceRefs, "Provisional/unknown cut length."));
            }
        }

        foreach (var piece in unknownRoutePieces.OrderBy(p => p.Id, StringComparer.Ordinal))
        {
            var reason = GetUnknownReason(piece.StartElevation, piece.EndElevation) ?? "Unknown elevation affects quantity.";
            unknownQuantities.Add(CreateUnknownPieceLine(RouteLedger, piece, sourceRefs, reason));
            unknownQuantities.Add(CreateUnknownPieceLine(CutLedger, piece, sourceRefs, reason));
        }

        // If no quantities at all, report explicit unknown totals rather than zero.
        if (knownQuantities.Count == 0 && provisionalQuantities.Count == 0 && unknownQuantities.Count == 0)
        {
            var reason = snapshot.Pieces.Count == 0 ? "No pieces in scope." : "All pieces excluded from scope.";
            unknownQuantities.Add(new SnapshotQuantityLine(RouteLedger, DefaultGroup, SiUnit.Meter.ToString(), null, SnapshotQuantityStatus.Unknown,
                reason: reason, sourceRefs: sourceRefs, methodVersion: DefaultMethodVersion, issueRefs: ["issue:snapshot:no-in-scope-quantities"]));
            unknownQuantities.Add(new SnapshotQuantityLine(CutLedger, DefaultGroup, SiUnit.Meter.ToString(), null, SnapshotQuantityStatus.Unknown,
                reason: reason, sourceRefs: sourceRefs, methodVersion: DefaultMethodVersion, issueRefs: ["issue:snapshot:no-in-scope-quantities"]));
            issues.Add("issue:snapshot:no-in-scope-quantities");
        }

        if (unknownQuantities.Count > 0)
        {
            issues.Add("issue:snapshot:partial-unknown");
        }

        var distinctIssues = issues.Distinct(StringComparer.Ordinal).ToArray();

        return new SnapshotManifest(
            snapshotId: snapshotId,
            projectId: snapshot.ProjectId.Value,
            projectRevision: snapshot.Revision,
            createdAtUtc: createdAtUtc,
            label: label,
            sourceRefs: sourceRefs.Distinct(StringComparer.Ordinal).ToArray(),
            methodVersion: DefaultMethodVersion,
            knownQuantities: knownQuantities,
            provisionalQuantities: provisionalQuantities,
            unknownQuantities: unknownQuantities,
            exclusions: exclusionSet.OrderBy(x => x, StringComparer.Ordinal).ToArray(),
            issues: distinctIssues);
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

    private static SnapshotQuantityLine CreateUnknownPieceLine(string ledger, RoutePiece piece, IReadOnlyList<string> snapshotSourceRefs, string reason)
    {
        var refs = MergeRefs(snapshotSourceRefs, piece.SourceRefs);
        var issueRefs = CollectIssueRefs(piece.StartElevation, piece.EndElevation);
        if (issueRefs.Count == 0)
            issueRefs = [$"issue:quantity-unknown:{ledger}:{piece.Id}"];
        return new SnapshotQuantityLine(
            ledger,
            piece.Id,
            SiUnit.Meter.ToString(),
            value: null,
            status: SnapshotQuantityStatus.Unknown,
            reason: reason,
            sourceRefs: refs,
            methodVersion: DefaultMethodVersion,
            issueRefs: issueRefs);
    }

    private static IReadOnlyList<string> MergeRefs(IReadOnlyList<string> a, IReadOnlyList<string> b)
    {
        var merged = new List<string>(a.Count + b.Count);
        merged.AddRange(a);
        merged.AddRange(b);
        return merged.Distinct(StringComparer.Ordinal).ToArray();
    }
}

