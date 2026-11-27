using OneClick.Core.Domain;
using OneClick.Core.Geometry;
using OneClick.Core.TaggedValues;
using OneClick.Core.Units;

namespace OneClick.Tests.Reference;

public sealed class RouteLedgerTests
{
    private static readonly string[] SourceRefs = ["test:route-ledger"];

    [Fact]
    public void ComputePiece_KnownElevations_ProducesRouteAndCutWithDistinctProvenance()
    {
        var piece = new RoutePiece(
            "P1",
            new Point3D(0, 0, 0),
            new Point3D(3, 4, 0),
            startNetOffsetM: 0.1,
            endNetOffsetM: 0.2,
            group: "test-group",
            sourceRefs: SourceRefs,
            methodVersion: "test-input.v1");

        var ledger = RouteLedgerCalculator.ComputePiece(piece);

        Assert.Equal("P1", ledger.PieceId);
        Assert.Equal("test-group", ledger.Group);
        Assert.Equal(RouteLedgerCalculator.RouteLedgerName, ledger.RouteLedger);
        Assert.Equal(RouteLedgerCalculator.CutLedgerName, ledger.CutLedger);

        var route = Assert.IsType<KnownValue>(ledger.RouteLength);
        var cut = Assert.IsType<KnownValue>(ledger.CutLength);

        Assert.Equal(5.0d, route.Value, precision: 10);
        Assert.Equal(4.7d, cut.Value, precision: 10);
        Assert.Equal(SiUnit.Meter, route.Unit);
        Assert.Equal(SiUnit.Meter, cut.Unit);

        // Provenance: route and cut carry distinct method versions.
        Assert.Equal(RouteLedgerCalculator.RouteMethodId, ledger.RouteMethodId);
        Assert.Equal(RouteLedgerCalculator.RouteMethodVersion, ledger.RouteMethodVersion);
        Assert.Equal(RouteLedgerCalculator.CutMethodId, ledger.CutMethodId);
        Assert.Equal(RouteLedgerCalculator.CutMethodVersion, ledger.CutMethodVersion);
        Assert.Equal(RouteLedgerCalculator.RouteMethodVersion, route.MethodVersion);
        Assert.Equal(RouteLedgerCalculator.CutMethodVersion, cut.MethodVersion);

        // Cut metadata is explicit and states limitation.
        Assert.Contains("synthetic", ledger.CutLimitation, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("net offset", ledger.CutLimitation, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("not a manufacturer takeout", ledger.CutLimitation, StringComparison.OrdinalIgnoreCase);

        Assert.Equal(SourceRefs, ledger.SourceRefs);
        Assert.Equal(SourceRefs, route.SourceRefs);
        Assert.Equal(SourceRefs, cut.SourceRefs);
    }

    [Fact]
    public void ComputePiece_UnknownElevation_PropagatesUnknownToRouteAndCut_NeverZero()
    {
        var piece = new RoutePiece(
            "P-UNK",
            new Point3D(0, 0, 0),
            new Point3D(1, 0, 0),
            startNetOffsetM: 0.1,
            endNetOffsetM: 0.1,
            group: "g",
            sourceRefs: SourceRefs,
            methodVersion: "test-input.v1",
            startElevation: new UnknownValue(SiUnit.Meter, SourceRefs, "test-input.v1", "Elevation unknown at start.", ["issue:elevation-unknown:P-UNK:start"]),
            endElevation: new KnownValue(0d, SiUnit.Meter, SourceRefs, "test-input.v1"));

        var ledger = RouteLedgerCalculator.ComputePiece(piece);

        var route = Assert.IsType<UnknownValue>(ledger.RouteLength);
        var cut = Assert.IsType<UnknownValue>(ledger.CutLength);

        Assert.Null(route.Value);
        Assert.Null(cut.Value);
        Assert.NotEqual(0d, route.Value ?? double.NaN); // explicit: no numeric zero
        Assert.NotEqual(0d, cut.Value ?? double.NaN);
        Assert.Equal(SiUnit.Meter, route.Unit);
        Assert.Equal(SiUnit.Meter, cut.Unit);
        Assert.Contains("issue:elevation-unknown:P-UNK:start", route.IssueRefs);
        Assert.Contains("issue:elevation-unknown:P-UNK:start", cut.IssueRefs);
        Assert.Equal(RouteLedgerCalculator.RouteMethodVersion, route.MethodVersion);
        Assert.Equal(RouteLedgerCalculator.CutMethodVersion, cut.MethodVersion);
        Assert.Contains("synthetic", cut.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Compute_TotalsAreUnknown_WhenAnyIncludedPieceIsUnknown_NeverZero()
    {
        var known = new RoutePiece("K1", new Point3D(0, 0, 0), new Point3D(3, 0, 0), 0, 0, "g", SourceRefs, "test.v1");
        var unknown = new RoutePiece(
            "U1",
            new Point3D(0, 0, 0),
            new Point3D(5, 0, 0),
            0, 0,
            "g",
            SourceRefs,
            "test.v1",
            startElevation: new KnownValue(0d, SiUnit.Meter, SourceRefs, "test.v1"),
            endElevation: new UnknownValue(SiUnit.Meter, SourceRefs, "test.v1", "Unknown end elevation.", ["issue:elevation-unknown:U1:end"]));

        var ledgers = RouteLedgerCalculator.Compute([known, unknown]);

        var routeTotal = Assert.IsType<UnknownValue>(ledgers.RouteTotal);
        var cutTotal = Assert.IsType<UnknownValue>(ledgers.CutTotal);
        Assert.Null(routeTotal.Value);
        Assert.Null(cutTotal.Value);
        Assert.Contains("unknown", routeTotal.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("unknown", cutTotal.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("issue:elevation-unknown:U1:end", ledgers.IssueIds);

        // Ledger separation is preserved in aggregate metadata.
        Assert.Equal(RouteLedgerCalculator.RouteLedgerName, ledgers.RouteLedger);
        Assert.Equal(RouteLedgerCalculator.CutLedgerName, ledgers.CutLedger);
        Assert.Equal(RouteLedgerCalculator.CutMethodId, ledgers.CutMethodId);
        Assert.Equal(RouteLedgerCalculator.CutMethodVersion, ledgers.CutMethodVersion);
        Assert.Contains("not a manufacturer takeout", ledgers.CutLimitation, StringComparison.OrdinalIgnoreCase);

        // Known piece still has known values; unknown piece is unknown.
        var k = ledgers.Pieces.Single(p => p.PieceId == "K1");
        var u = ledgers.Pieces.Single(p => p.PieceId == "U1");
        Assert.IsType<KnownValue>(k.RouteLength);
        Assert.IsType<KnownValue>(k.CutLength);
        Assert.IsType<UnknownValue>(u.RouteLength);
        Assert.IsType<UnknownValue>(u.CutLength);
    }

    [Fact]
    public void Compute_RouteAndCutLedgers_AreSeparateAndCarryExplicitProvenance()
    {
        var piece = new RoutePiece("P1", new Point3D(0, 0, 0), new Point3D(10, 0, 0), 0.5, 0.25, "g", SourceRefs, "test.v1");
        var ledgers = RouteLedgerCalculator.Compute([piece]);

        var routeTotal = Assert.IsType<KnownValue>(ledgers.RouteTotal);
        var cutTotal = Assert.IsType<KnownValue>(ledgers.CutTotal);

        Assert.Equal(10.0d, routeTotal.Value, precision: 10);
        Assert.Equal(9.25d, cutTotal.Value, precision: 10);
        Assert.NotEqual(routeTotal.Value, cutTotal.Value);

        Assert.Equal(RouteLedgerCalculator.RouteMethodVersion, routeTotal.MethodVersion);
        Assert.Equal(RouteLedgerCalculator.CutMethodVersion, cutTotal.MethodVersion);
        Assert.Equal(RouteLedgerCalculator.RouteMethodId, ledgers.RouteMethodId);
        Assert.Equal(RouteLedgerCalculator.CutMethodId, ledgers.CutMethodId);
        Assert.Equal(SourceRefs, routeTotal.SourceRefs);
        Assert.Equal(SourceRefs, cutTotal.SourceRefs);
        Assert.NotEqual(ledgers.RouteMethodVersion, ledgers.CutMethodVersion);
    }

    [Fact]
    public void ComputePiece_UnknownRouteLengthFallback_PropagatesToCutAsUnknown()
    {
        // This uses invalid/non-finite elevation value typed as KnownValue? KnownValue guards finite.
        // Instead, use a case where elevation is known but route computation can't produce KnownValue is hard via Point3D.
        // We verify the invariant through unknown elevation path: cut is never known when route is unknown.
        var piece = new RoutePiece(
            "P-BOTH-UNK",
            new Point3D(0, 0, 0),
            new Point3D(1, 0, 0),
            0, 0, "g", SourceRefs, "test.v1",
            startElevation: new UnknownValue(SiUnit.Meter, SourceRefs, "test.v1", "start unknown", ["issue:elevation-unknown:P-BOTH-UNK:start"]),
            endElevation: new UnknownValue(SiUnit.Meter, SourceRefs, "test.v1", "end unknown", ["issue:elevation-unknown:P-BOTH-UNK:end"]));

        var ledger = RouteLedgerCalculator.ComputePiece(piece);

        Assert.IsType<UnknownValue>(ledger.RouteLength);
        Assert.IsType<UnknownValue>(ledger.CutLength);
        var cut = (UnknownValue)ledger.CutLength;
        Assert.Contains("issue:elevation-unknown:P-BOTH-UNK:start", cut.IssueRefs);
        Assert.Contains("issue:elevation-unknown:P-BOTH-UNK:end", cut.IssueRefs);
    }
}
