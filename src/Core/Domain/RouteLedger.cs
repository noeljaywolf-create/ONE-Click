using OneClick.Core.Formulas;
using OneClick.Core.Geometry;
using OneClick.Core.TaggedValues;
using OneClick.Core.Units;

namespace OneClick.Core.Domain;

/// <summary>
/// Deterministic route/cut ledger for a single piece in the first build slice.
/// Lengths are tagged values: unknown elevations propagate to unknown spatial lengths/cuts.
/// Unknown is not zero.
/// </summary>
public sealed record RoutePieceLedger(
    string PieceId,
    string Group,
    TaggedValue RouteLength,
    TaggedValue CutLength,
    IReadOnlyList<string> SourceRefs,
    string MethodVersion,
    string RouteMethodId = RouteLedgerCalculator.RouteMethodId,
    string RouteMethodVersion = RouteLedgerCalculator.RouteMethodVersion,
    string CutMethodId = RouteLedgerCalculator.CutMethodId,
    string CutMethodVersion = RouteLedgerCalculator.CutMethodVersion,
    string CutLimitation = RouteLedgerCalculator.CutLimitation)
{
    public string RouteLedger => RouteLedgerCalculator.RouteLedgerName;
    public string CutLedger => RouteLedgerCalculator.CutLedgerName;
}

/// <summary>
/// Aggregate route/cut ledger snapshot. Route and cut ledgers are distinct.
/// </summary>
public sealed record ProjectRouteLedgers(
    IReadOnlyList<RoutePieceLedger> Pieces,
    TaggedValue RouteTotal,
    TaggedValue CutTotal,
    IReadOnlyList<string> IssueIds,
    string RouteMethodId = RouteLedgerCalculator.RouteMethodId,
    string RouteMethodVersion = RouteLedgerCalculator.RouteMethodVersion,
    string CutMethodId = RouteLedgerCalculator.CutMethodId,
    string CutMethodVersion = RouteLedgerCalculator.CutMethodVersion,
    string CutLimitation = RouteLedgerCalculator.CutLimitation)
{
    public string RouteLedger => RouteLedgerCalculator.RouteLedgerName;
    public string CutLedger => RouteLedgerCalculator.CutLedgerName;
}

/// <summary>
/// Computes deterministic route and fabrication-cut ledgers from route pieces.
/// Uses tagged elevations as authoritative inputs; unknown elevations produce unknown lengths.
/// Route and synthetic-cut ledgers are kept distinct and carry explicit method/version/source provenance.
/// </summary>
public static class RouteLedgerCalculator
{
    public const string RouteLedgerName = "route";
    public const string CutLedgerName = "synthetic-cut";

    /// <summary>Overall ledger calculator method version for backward-compatible provenance.</summary>
    public const string MethodVersion = "route-ledger.v2";

    /// <summary>Route spatial length uses G01 straight-segment geometry on known finite elevations.</summary>
    public const string RouteMethodId = "G01";
    public const string RouteMethodVersion = "G01.1.0";

    /// <summary>
    /// Synthetic cut length: route length minus declared net end offsets.
    /// This is fixture-defined synthetic arithmetic, not a manufacturer takeout claim.
    /// </summary>
    public const string CutMethodId = "synthetic-net-offset-cut";
    public const string CutMethodVersion = "synthetic-net-offset-cut.v1";
    public const string CutLimitation =
        "Synthetic cut length = route spatial length - start net offset - end net offset. " +
        "Uses declared synthetic net offsets only; not a manufacturer takeout, fitting allowance, kerf, " +
        "or assembly claim. Unknown elevation propagates to unknown route and cut; never zero.";

    public static ProjectRouteLedgers Compute(IEnumerable<RoutePiece> pieces)
    {
        ArgumentNullException.ThrowIfNull(pieces);

        var pieceLedgers = new List<RoutePieceLedger>();
        var issues = new List<string>();
        var routeKnown = true;
        var cutKnown = true;
        double routeTotal = 0d;
        double cutTotal = 0d;
        var allRefs = new List<string>();

        foreach (var piece in pieces)
        {
            ArgumentNullException.ThrowIfNull(piece);
            var ledger = ComputePiece(piece);
            pieceLedgers.Add(ledger);

            AddRefs(allRefs, ledger.SourceRefs);
            CollectIssues(issues, ledger.RouteLength);
            CollectIssues(issues, ledger.CutLength);

            if (ledger.RouteLength is KnownValue rk)
                routeTotal += rk.Value;
            else
                routeKnown = false;

            if (ledger.CutLength is KnownValue ck)
                cutTotal += ck.Value;
            else
                cutKnown = false;
        }

        var refs = allRefs.Distinct(StringComparer.Ordinal).ToArray();
        if (refs.Length == 0) refs = ["ledger:empty"];

        var routeTotalValue = routeKnown
            ? (TaggedValue)new KnownValue(routeTotal, SiUnit.Meter, refs, RouteMethodVersion)
            : new UnknownValue(
                SiUnit.Meter,
                refs,
                RouteMethodVersion,
                "Route total is unknown because one or more piece route lengths are unknown.",
                issues.Where(i => i.Contains("elevation-unknown", StringComparison.Ordinal)).Distinct(StringComparer.Ordinal).ToArray());

        var cutTotalValue = cutKnown
            ? (TaggedValue)new KnownValue(cutTotal, SiUnit.Meter, refs, CutMethodVersion)
            : new UnknownValue(
                SiUnit.Meter,
                refs,
                CutMethodVersion,
                "Synthetic cut total is unknown because one or more piece cut lengths are unknown. " + CutLimitation,
                issues.Where(i => i.Contains("elevation-unknown", StringComparison.Ordinal)).Distinct(StringComparer.Ordinal).ToArray());

        return new ProjectRouteLedgers(
            pieceLedgers,
            routeTotalValue,
            cutTotalValue,
            issues.Distinct(StringComparer.Ordinal).ToArray(),
            RouteMethodId,
            RouteMethodVersion,
            CutMethodId,
            CutMethodVersion,
            CutLimitation);
    }

    public static RoutePieceLedger ComputePiece(RoutePiece piece)
    {
        ArgumentNullException.ThrowIfNull(piece);

        var refs = piece.SourceRefs.Count > 0 ? piece.SourceRefs : new[] { $"piece:{piece.Id}" };
        var unknownReasons = new List<string>();
        var unknownIssues = new List<string>();

        var startZ = ResolveElevation(piece.StartElevation, "start", piece.Id, unknownReasons, unknownIssues);
        var endZ = ResolveElevation(piece.EndElevation, "end", piece.Id, unknownReasons, unknownIssues);

        if (startZ is null || endZ is null)
        {
            var reason = unknownReasons.Count > 0
                ? string.Join(" ", unknownReasons)
                : $"Piece '{piece.Id}' has unknown elevation.";
            var issueRefs = unknownIssues.Distinct(StringComparer.Ordinal).ToArray();
            var unknownRoute = new UnknownValue(SiUnit.Meter, refs, RouteMethodVersion, reason, issueRefs);
            var unknownCut = new UnknownValue(
                SiUnit.Meter,
                refs,
                CutMethodVersion,
                reason + " " + CutLimitation,
                issueRefs);
            return new RoutePieceLedger(
                piece.Id,
                piece.Group,
                unknownRoute,
                unknownCut,
                refs,
                MethodVersion,
                RouteMethodId,
                RouteMethodVersion,
                CutMethodId,
                CutMethodVersion,
                CutLimitation);
        }

        var start = new Point3D(piece.StartM.X, piece.StartM.Y, startZ.Value);
        var end = new Point3D(piece.EndM.X, piece.EndM.Y, endZ.Value);
        var route = GeometryFormulas.StraightSegmentLength(start, end, refs);
        if (route is not KnownValue routeKnown)
        {
            var unknown = new UnknownValue(
                SiUnit.Meter,
                refs,
                RouteMethodVersion,
                $"Route length could not be computed for piece '{piece.Id}'.",
                [$"issue:route-length-unknown:{piece.Id}"]);
            var unknownCut = new UnknownValue(
                SiUnit.Meter,
                refs,
                CutMethodVersion,
                $"Synthetic cut length could not be computed for piece '{piece.Id}' because route length is unknown. " + CutLimitation,
                [$"issue:route-length-unknown:{piece.Id}"]);
            return new RoutePieceLedger(
                piece.Id,
                piece.Group,
                unknown,
                unknownCut,
                refs,
                MethodVersion,
                RouteMethodId,
                RouteMethodVersion,
                CutMethodId,
                CutMethodVersion,
                CutLimitation);
        }

        var cutLength = routeKnown.Value - piece.StartNetOffsetM - piece.EndNetOffsetM;
        var cutValue = new KnownValue(cutLength, SiUnit.Meter, refs, CutMethodVersion);
        return new RoutePieceLedger(
            piece.Id,
            piece.Group,
            routeKnown,
            cutValue,
            refs,
            MethodVersion,
            RouteMethodId,
            RouteMethodVersion,
            CutMethodId,
            CutMethodVersion,
            CutLimitation);
    }

    private static double? ResolveElevation(
        TaggedValue elevation,
        string endName,
        string pieceId,
        List<string> reasons,
        List<string> issues)
    {
        switch (elevation)
        {
            case KnownValue known when known.Unit == SiUnit.Meter && double.IsFinite(known.Value):
                return known.Value;
            case KnownValue known:
                reasons.Add($"{endName} elevation for piece '{pieceId}' is not a finite meter value.");
                issues.Add($"issue:elevation-invalid:{pieceId}:{endName}");
                return null;
            case UnknownValue unknown:
                reasons.Add($"Elevation is unknown for {endName} of piece '{pieceId}'.");
                foreach (var issue in unknown.IssueRefs)
                    issues.Add(issue);
                if (unknown.IssueRefs.Count == 0)
                    issues.Add($"issue:elevation-unknown:{pieceId}:{endName}");
                return null;
            case ProvisionalValue provisional when provisional.Unit == SiUnit.Meter && double.IsFinite(provisional.Value):
                // Provisional is not unknown; it can be used for computation but remains provisional by origin.
                // For this slice we treat provisional numeric values as computable inputs.
                return provisional.Value;
            case ProvisionalValue provisional:
                reasons.Add($"{endName} elevation for piece '{pieceId}' is provisional but not a finite meter value.");
                issues.Add($"issue:elevation-invalid:{pieceId}:{endName}");
                return null;
            default:
                reasons.Add($"{endName} elevation for piece '{pieceId}' has unsupported tagged value type '{elevation.GetType().Name}'.");
                issues.Add($"issue:elevation-invalid:{pieceId}:{endName}");
                return null;
        }
    }

    private static void CollectIssues(List<string> issues, TaggedValue value)
    {
        if (value is UnknownValue unknown)
        {
            foreach (var issue in unknown.IssueRefs)
            {
                if (!issues.Contains(issue, StringComparer.Ordinal))
                    issues.Add(issue);
            }
        }
    }

    private static void AddRefs(List<string> target, IReadOnlyList<string> refs)
    {
        foreach (var r in refs)
        {
            if (!string.IsNullOrWhiteSpace(r))
                target.Add(r);
        }
    }
}