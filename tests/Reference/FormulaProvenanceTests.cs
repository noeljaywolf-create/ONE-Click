using OneClick.Core.Formulas;
using OneClick.Core.Geometry;
using OneClick.Core.Provenance;
using OneClick.Core.TaggedValues;
using OneClick.Core.Units;

using FormulaQuantityContribution = OneClick.Core.Formulas.QuantityContribution;

namespace OneClick.Tests.Reference;

public sealed class FormulaProvenanceTests
{
    private static readonly IReadOnlyList<string> SourceRefs = ["obs:1"];
    private static readonly IReadOnlyList<InputReference> Inputs =
    [
        new InputReference("project", "p1", "rev-1"),
        new InputReference("document-revision", "d1", "sha256:abc"),
    ];
    private static readonly IReadOnlyList<QuantityContributionId> ContributionIds =
    [
        new QuantityContributionId("qc-1"),
    ];

    [Fact]
    public void G01_StraightSegmentLength_ProvenanceOverload_WrapsValueAndProvenance()
    {
        var result = GeometryFormulas.StraightSegmentLength(
            new Point3D(0, 0, 0),
            new Point3D(3, 4, 12),
            SourceRefs,
            Inputs,
            ContributionIds);

        Assert.IsType<DerivedResult<KnownValue>>(result);
        var known = Assert.IsType<KnownValue>(result.Value);
        Assert.Equal(13d, known.Value);
        Assert.Equal("G01.1.0", result.Provenance.MethodVersion);
        Assert.Equal(SourceRefs, result.Provenance.SourceRefs);
        Assert.Equal(Inputs, result.Provenance.Inputs);
        Assert.Equal(ContributionIds, result.Provenance.ContributionIds);
    }

    [Fact]
    public void G01_UnknownElevation_ProvenancePreservesUnknown()
    {
        var result = GeometryFormulas.StraightSegmentLength(
            new SpatialEndpoint(new Point2D(0, 0), 0d),
            new SpatialEndpoint(new Point2D(3, 4), null),
            SourceRefs,
            Inputs,
            ContributionIds);

        var unknown = Assert.IsType<UnknownValue>(result.Value);
        Assert.Null(unknown.Value);
        Assert.Contains("elevation:unknown:end", unknown.IssueRefs);
        Assert.Equal("G01.1.0", result.Provenance.MethodVersion);
        Assert.Equal(Inputs, result.Provenance.Inputs);
        Assert.Equal(ContributionIds, result.Provenance.ContributionIds);
    }

    [Fact]
    public void G02_PolylineLength_ProvenanceOverload_WrapsValue()
    {
        var vertices = new[]
        {
            new Point3D(0, 0, 0),
            new Point3D(3, 0, 0),
            new Point3D(3, 4, 0),
        };

        var result = GeometryFormulas.PolylineLength(vertices, SourceRefs, Inputs, ContributionIds);

        var known = Assert.IsType<KnownValue>(result.Value);
        Assert.Equal(7d, known.Value);
        Assert.Equal("G02.1.0", result.Provenance.MethodVersion);
        Assert.Equal(Inputs, result.Provenance.Inputs);
    }

    [Fact]
    public void G03_DirectionAngle_ProvenanceOverload_WrapsValue()
    {
        var result = GeometryFormulas.DirectionAngle(
            new Vector3D(1, 0, 0),
            new Vector3D(0, 1, 0),
            SourceRefs,
            Inputs,
            ContributionIds);

        var known = Assert.IsType<KnownValue>(result.Value);
        Assert.Equal(Math.PI / 2d, known.Value, precision: 12);
        Assert.Equal("G03.1.0", result.Provenance.MethodVersion);
        Assert.Equal(SourceRefs, result.Provenance.SourceRefs);
    }

    [Fact]
    public void G04_PointSegmentClosestPoint_ProvenanceOverload_WrapsResult()
    {
        var result = GeometryFormulas.PointSegmentClosestPoint(
            new Point3D(2, 1, 0),
            new Point3D(0, 0, 0),
            new Point3D(1, 0, 0),
            SourceRefs,
            Inputs,
            ContributionIds);

        Assert.IsType<DerivedResult<SegmentClosestPointResult>>(result);
        Assert.Equal(Math.Sqrt(2d), result.Value.Distance.Value, precision: 12);
        Assert.Equal("G04.1.0", result.Provenance.MethodVersion);
        Assert.Equal(Inputs, result.Provenance.Inputs);
        Assert.Equal(ContributionIds, result.Provenance.ContributionIds);
    }

    [Fact]
    public void G07_ArcLength_ProvenanceOverload_WrapsValueAndPreservesUnknown()
    {
        var known = GeometryFormulas.ArcLength(2d, Math.PI / 2d, SourceRefs, Inputs, ContributionIds);
        Assert.Equal(Math.PI, Assert.IsType<KnownValue>(known.Value).Value, precision: 12);
        Assert.Equal("G07.1.0", known.Provenance.MethodVersion);

        var unknownRadius = new UnknownValue(SiUnit.Meter, SourceRefs, "src", "missing radius", ["radius:missing"]);
        var knownSweep = new KnownValue(Math.PI / 2d, SiUnit.Radian, SourceRefs, "src");
        var unknown = GeometryFormulas.ArcLength(unknownRadius, knownSweep, SourceRefs, Inputs, ContributionIds);
        var unknownValue = Assert.IsType<UnknownValue>(unknown.Value);
        Assert.Null(unknownValue.Value);
        Assert.Contains("radius:unknown", unknownValue.IssueRefs);
        Assert.Equal("G07.1.0", unknown.Provenance.MethodVersion);
    }

    [Fact]
    public void Q01_SumUniqueOwnedContributions_ProvenanceOverload_WrapsResult()
    {
        var contributions = new[]
        {
            new FormulaQuantityContribution("a", "route", "main", 1d, SiUnit.Meter),
            new FormulaQuantityContribution("b", "route", "main", 2d, SiUnit.Meter),
        };

        var result = QuantityFormulas.SumUniqueOwnedContributions(
            contributions, "route", "main", SiUnit.Meter, SourceRefs, Inputs, ContributionIds);

        Assert.IsType<DerivedResult<QuantityAggregateResult>>(result);
        Assert.Equal(3d, result.Value.Total.Value);
        Assert.Equal("Q01.1.0", result.Provenance.MethodVersion);
        Assert.Equal(Inputs, result.Provenance.Inputs);
        Assert.Equal(ContributionIds, result.Provenance.ContributionIds);
    }

    [Fact]
    public void Q02_AllowanceAndPacks_ProvenanceOverloads_WrapResults()
    {
        var basis = new KnownValue(10d, SiUnit.Meter, SourceRefs, "src");
        var allowance = QuantityFormulas.Allowance(basis, 0.1d, "waste:10pct", SourceRefs, Inputs, ContributionIds);
        Assert.Equal(1d, Assert.IsType<KnownValue>(allowance.Value).Value, precision: 12);
        Assert.Equal("Q02.1.0", allowance.Provenance.MethodVersion);

        var required = new KnownValue(13d, SiUnit.Dimensionless, SourceRefs, "src");
        var packs = QuantityFormulas.Packs(required, 5d, "pack-policy:box-5", SourceRefs, Inputs, ContributionIds);
        Assert.Equal(3d, Assert.IsType<KnownValue>(packs.Value.Packs).Value);
        Assert.Equal("Q02.1.0", packs.Provenance.MethodVersion);
        Assert.Equal(Inputs, packs.Provenance.Inputs);
    }

    [Fact]
    public void Q02_UnknownBasis_ProvenancePreservesUnknown()
    {
        var unknownBasis = new UnknownValue(SiUnit.Meter, SourceRefs, "src", "missing basis", ["basis:missing"]);
        var allowance = QuantityFormulas.Allowance(unknownBasis, 0.1d, "waste:10pct", SourceRefs, Inputs, ContributionIds);

        var unknown = Assert.IsType<UnknownValue>(allowance.Value);
        Assert.Null(unknown.Value);
        Assert.Contains("basis:unknown", unknown.IssueRefs);
        Assert.Equal("Q02.1.0", allowance.Provenance.MethodVersion);
    }

    [Fact]
    public void Q03_PipeMetalMass_ProvenanceOverload_WrapsResult()
    {
        var od = new KnownValue(2d, SiUnit.Meter, SourceRefs, "src");
        var id = new KnownValue(1d, SiUnit.Meter, SourceRefs, "src");
        var l = new KnownValue(1d, SiUnit.Meter, SourceRefs, "src");
        var rho = new KnownValue(1d, SiUnit.KilogramPerCubicMeter, SourceRefs, "src");

        var result = QuantityFormulas.PipeMetalMass(od, id, l, rho, SourceRefs, Inputs, ContributionIds);

        Assert.IsType<DerivedResult<PipeMetalMassResult>>(result);
        var expected = Math.PI / 4d * (4d - 1d) * 1d;
        Assert.Equal(expected, Assert.IsType<KnownValue>(result.Value.MetalVolume).Value, precision: 12);
        Assert.Equal("Q03.1.0", result.Provenance.MethodVersion);
        Assert.Equal(Inputs, result.Provenance.Inputs);
        Assert.Equal(ContributionIds, result.Provenance.ContributionIds);
    }

    [Fact]
    public void Q03_UnknownInput_ProvenancePreservesUnknown()
    {
        var od = new UnknownValue(SiUnit.Meter, SourceRefs, "src", "missing od", ["od:missing"]);
        var id = new KnownValue(1d, SiUnit.Meter, SourceRefs, "src");
        var l = new KnownValue(1d, SiUnit.Meter, SourceRefs, "src");
        var rho = new KnownValue(7850d, SiUnit.KilogramPerCubicMeter, SourceRefs, "src");

        var result = QuantityFormulas.PipeMetalMass(od, id, l, rho, SourceRefs, Inputs, ContributionIds);

        Assert.IsType<UnknownValue>(result.Value.MetalVolume);
        Assert.IsType<UnknownValue>(result.Value.Mass);
        Assert.Equal("Q03.1.0", result.Provenance.MethodVersion);
        Assert.Equal(Inputs, result.Provenance.Inputs);
    }

    [Fact]
    public void FormulaProvenance_WrapTagged_UsesTaggedValueMetadata()
    {
        var tagged = new KnownValue(5d, SiUnit.Meter, ["obs:a"], "G01.1.0");
        var wrapped = FormulaProvenance.WrapTagged(tagged, Inputs, ContributionIds);

        Assert.Same(tagged, wrapped.Value);
        Assert.Equal("G01.1.0", wrapped.Provenance.MethodVersion);
        Assert.Equal(tagged.SourceRefs, wrapped.Provenance.SourceRefs);
        Assert.Equal(Inputs, wrapped.Provenance.Inputs);
        Assert.Equal(ContributionIds, wrapped.Provenance.ContributionIds);
    }

    [Fact]
    public void FormulaProvenance_Wrap_UsesExplicitMetadata()
    {
        var value = new object();
        var wrapped = FormulaProvenance.Wrap(value, "M1.0.0", Inputs, ["s1"], ContributionIds);

        Assert.Same(value, wrapped.Value);
        Assert.Equal("M1.0.0", wrapped.Provenance.MethodVersion);
        Assert.Equal(["s1"], wrapped.Provenance.SourceRefs);
    }

    [Fact]
    public void Q04_StockLengthLowerBound_ProvenanceOverload_WrapsResult()
    {
        var cuts = new TaggedValue[]
        {
            new KnownValue(4d, SiUnit.Meter, SourceRefs, "src"),
            new KnownValue(4d, SiUnit.Meter, SourceRefs, "src"),
            new KnownValue(4d, SiUnit.Meter, SourceRefs, "src"),
        };

        var result = QuantityFormulas.StockLengthLowerBound(cuts, 6d, SourceRefs, Inputs, ContributionIds);

        Assert.IsType<DerivedResult<StockLowerBoundResult>>(result);
        Assert.Equal(2d, Assert.IsType<KnownValue>(result.Value.Sticks).Value); // lower bound only
        Assert.Equal("Q04.1.0", result.Provenance.MethodVersion);
        Assert.Equal(Inputs, result.Provenance.Inputs);
        Assert.Equal(ContributionIds, result.Provenance.ContributionIds);
        Assert.Contains("lower bound", result.Value.Limitation, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Q04_UnknownCutLength_ProvenancePreservesUnknown()
    {
        var cuts = new TaggedValue[]
        {
            new KnownValue(4d, SiUnit.Meter, SourceRefs, "src"),
            new UnknownValue(SiUnit.Meter, SourceRefs, "src", "missing", ["cut:missing"]),
        };

        var result = QuantityFormulas.StockLengthLowerBound(cuts, 6d, SourceRefs, Inputs, ContributionIds);

        Assert.IsType<UnknownValue>(result.Value.Sticks);
        Assert.IsType<UnknownValue>(result.Value.TotalCutLength);
        Assert.Equal("Q04.1.0", result.Provenance.MethodVersion);
    }

    [Fact]
    public void A03_InterfaceResidual_ProvenanceOverload_WrapsResult()
    {
        var clocking = new UnknownValue(SiUnit.Radian, SourceRefs, "src", "not evaluated", ["clocking:not-evaluated"]);
        var result = AssemblyFormulas.InterfaceResidual(
            new Point3D(0, 0, 0),
            new Point3D(0, 0, 0),
            new Vector3D(0, 0, 1),
            new Vector3D(0, 0, -1),
            clocking,
            SourceRefs,
            Inputs,
            ContributionIds);

        Assert.IsType<DerivedResult<AssemblyResidualResult>>(result);
        Assert.Equal(0d, result.Value.PositionResidual.Value, precision: 12);
        Assert.Equal(0d, result.Value.AngularResidual.Value, precision: 12);
        Assert.Equal("A03.1.0", result.Provenance.MethodVersion);
        Assert.Equal(Inputs, result.Provenance.Inputs);
        Assert.Equal(ContributionIds, result.Provenance.ContributionIds);
    }
}
