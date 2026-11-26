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
    string MethodVersion);

/// <summary>
/// Aggregate route/cut ledger snapshot. Route and cut ledgers are distinct.
/// </summary>
public sealed record ProjectRouteLedgers(
    IReadOnlyList<RoutePieceLedger> Pieces,
    TaggedValue RouteTotal,
    TaggedValue CutTotal,
    IReadOnlyList<string> IssueIds);

/// <summary>
/// Computes deterministic route and fabrication-cut ledgers from route pieces.
/// Uses tagged elevations as authoritative inputs; unknown elevations produce unknown lengths.
/// </summary>
public static class RouteLedgerCalculator
{
    public const string MethodVersion = "route-ledger.v1";

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
            ? (TaggedValue)new KnownValue(routeTotal, SiUnit.Meter, refs, MethodVersion)
            : new UnknownValue(
                SiUnit.Meter,
                refs,
                MethodVersion,
                "Route total is unknown because one or more piece lengths are unknown.",
                issues.Where(i => i.Contains("elevation-unknown", StringComparison.Ordinal)).Distinct(StringComparer.Ordinal).ToArray());

        var cutTotalValue = cutKnown
            ? (TaggedValue)new KnownValue(cutTotal, SiUnit.Meter, refs, MethodVersion)
            : new UnknownValue(
                SiUnit.Meter,
                refs,
                MethodVersion,
                "Cut total is unknown because one or more piece cut lengths are unknown.",
                issues.Where(i => i.Contains("elevation-unknown", StringComparison.Ordinal)).Distinct(StringComparer.Ordinal).ToArray());

        return new ProjectRouteLedgers(
            pieceLedgers,
            routeTotalValue,
            cutTotalValue,
            issues.Distinct(StringComparer.Ordinal).ToArray());
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
            var unknownRoute = new UnknownValue(SiUnit.Meter, refs, MethodVersion, reason, issueRefs);
            var unknownCut = new UnknownValue(SiUnit.Meter, refs, MethodVersion, reason, issueRefs);
            return new RoutePieceLedger(piece.Id, piece.Group, unknownRoute, unknownCut, refs, MethodVersion);
        }

        var start = new Point3D(piece.StartM.X, piece.StartM.Y, startZ.Value);
        var end = new Point3D(piece.EndM.X, piece.EndM.Y, endZ.Value);
        var route = GeometryFormulas.StraightSegmentLength(start, end, refs);
        if (route is not KnownValue routeKnown)
        {
            var unknown = new UnknownValue(SiUnit.Meter, refs, MethodVersion,
                $"Route length could not be computed for piece '{piece.Id}'.",
                [$"issue:route-length-unknown:{piece.Id}"]);
            return new RoutePieceLedger(piece.Id, piece.Group, unknown, unknown, refs, MethodVersion);
        }

        var cutLength = routeKnown.Value - piece.StartNetOffsetM - piece.EndNetOffsetM;
        var cutValue = new KnownValue(cutLength, SiUnit.Meter, refs, MethodVersion);
        return new RoutePieceLedger(piece.Id, piece.Group, routeKnown, cutValue, refs, MethodVersion);
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