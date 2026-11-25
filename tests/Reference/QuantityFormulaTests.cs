using OneClick.Core.Formulas;
using OneClick.Core.TaggedValues;
using OneClick.Core.Units;

namespace OneClick.Tests.Reference;

public sealed class QuantityFormulaTests
{
    private static readonly string[] Refs = ["obs:qty:1"];

    [Fact]
    public void Q01_IndependentCase_RouteVsSyntheticCut_AreDistinctLedgerTotals()
    {
        var contributions = new[]
        {
            new QuantityContribution("route-a", "route", "main", 12m.ToDouble(), SiUnit.Meter),
            new QuantityContribution("route-b", "route", "main", 9m.ToDouble(), SiUnit.Meter),
            new QuantityContribution("cut-a", "synthetic-cut", "main", 10.50m.ToDouble(), SiUnit.Meter),
            new QuantityContribution("cut-b", "synthetic-cut", "main", 9.27m.ToDouble(), SiUnit.Meter),
        };

        var route = QuantityFormulas.SumUniqueOwnedContributions(contributions, "route", "main", SiUnit.Meter, Refs);
        var cut = QuantityFormulas.SumUniqueOwnedContributions(contributions, "synthetic-cut", "main", SiUnit.Meter, Refs);

        Assert.Equal(21d, route.Total.Value);
        Assert.Equal(19.77d, cut.Total.Value, precision: 10);
        Assert.Equal("Q01.1.0", route.Total.MethodVersion);
        Assert.Equal(SiUnit.Meter, route.Total.Unit);
    }

    [Fact]
    public void Q01_DeduplicatesAssemblyOwnership_WithoutSilentExclusion()
    {
        var contributions = new[]
        {
            new QuantityContribution("asm-1", "procurement", "hanger", 1d, SiUnit.Meter),
            new QuantityContribution("part-1", "procurement", "hanger", 0.4d, SiUnit.Meter, includedByOwnerId: "asm-1"),
            new QuantityContribution("part-2", "procurement", "hanger", 0.6d, SiUnit.Meter, includedByOwnerId: "asm-1"),
        };

        var result = QuantityFormulas.SumUniqueOwnedContributions(contributions, "procurement", "hanger", SiUnit.Meter, Refs);

        Assert.Equal(1d, result.Total.Value);
        Assert.Contains("asm-1", result.CountedOwnerIds);
        Assert.Contains("part-1", result.ExcludedOwnerIds);
        Assert.Contains("part-2", result.ExcludedOwnerIds);
        Assert.NotEmpty(result.Warnings);
        Assert.Contains(result.Warnings, w => w.Contains("part-1", StringComparison.Ordinal) && w.Contains("included-by:asm-1", StringComparison.Ordinal));
    }

    [Fact]
    public void Q01_MixedUnits_FailsExplicitly()
    {
        var contributions = new[]
        {
            new QuantityContribution("a", "route", "main", 1d, SiUnit.Meter),
            new QuantityContribution("b", "route", "main", 1d, SiUnit.Kilogram),
        };

        var ex = Assert.Throws<InvalidOperationException>(() =>
            QuantityFormulas.SumUniqueOwnedContributions(contributions, "route", "main", SiUnit.Meter, Refs));

        Assert.Contains("mixed units", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Q01_AmbiguousDuplicateOwner_FailsExplicitly()
    {
        var contributions = new[]
        {
            new QuantityContribution("a", "route", "main", 1d, SiUnit.Meter),
            new QuantityContribution("a", "route", "main", 2d, SiUnit.Meter),
        };

        var ex = Assert.Throws<InvalidOperationException>(() =>
            QuantityFormulas.SumUniqueOwnedContributions(contributions, "route", "main", SiUnit.Meter, Refs));

        Assert.Contains("ambiguous duplicate ownership", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Q02_Packs_IndependentCase_Required13Pack5_Returns3Packs15Units()
    {
        var required = new KnownValue(13d, SiUnit.Dimensionless, Refs, "src");
        var result = QuantityFormulas.Packs(required, 5d, "pack-policy:box-5", Refs);

        var packs = Assert.IsType<KnownValue>(result.Packs);
        var total = Assert.IsType<KnownValue>(result.TotalUnits);
        Assert.Equal(3d, packs.Value);
        Assert.Equal(SiUnit.Dimensionless, packs.Unit);
        Assert.Equal(15d, total.Value);
        Assert.Equal(SiUnit.Dimensionless, total.Unit);
        Assert.Equal("Q02.1.0", packs.MethodVersion);
        Assert.Equal("pack-policy:box-5", result.PackPolicy);
    }

    [Fact]
    public void Q02_Allowance_ExplicitWAndPolicy_ReturnsTypedResult()
    {
        var basis = new KnownValue(10d, SiUnit.Meter, Refs, "src");
        var result = QuantityFormulas.Allowance(basis, 0.1d, "waste:10pct", Refs);

        var known = Assert.IsType<KnownValue>(result);
        Assert.Equal(1d, known.Value, precision: 12);
        Assert.Equal(SiUnit.Meter, known.Unit);
        Assert.Equal("Q02.1.0", known.MethodVersion);
    }

    [Fact]
    public void Q02_UnknownBasis_ReturnsUnknown()
    {
        var unknownBasis = new UnknownValue(SiUnit.Meter, Refs, "src", "missing basis", ["basis:missing"]);
        var allowance = QuantityFormulas.Allowance(unknownBasis, 0.1d, "waste:10pct", Refs);

        var unknownAllowance = Assert.IsType<UnknownValue>(allowance);
        Assert.Equal(SiUnit.Meter, unknownAllowance.Unit);
        Assert.Equal("Q02.1.0", unknownAllowance.MethodVersion);
        Assert.Contains("basis:unknown", unknownAllowance.IssueRefs);

        var packs = QuantityFormulas.Packs(unknownBasis, 5d, "pack-policy:box-5", Refs);
        Assert.IsType<UnknownValue>(packs.Packs);
        Assert.IsType<UnknownValue>(packs.TotalUnits);
    }

    [Fact]
    public void Q02_InvalidPackSizeOrPolicy_FailsExplicitly()
    {
        var required = new KnownValue(13d, SiUnit.Dimensionless, Refs, "src");
        Assert.Throws<ArgumentOutOfRangeException>(() => QuantityFormulas.Packs(required, 0d, "pack-policy:box-5", Refs));
        Assert.Throws<ArgumentException>(() => QuantityFormulas.Packs(required, 5d, " ", Refs));
    }

    [Fact]
    public void Q03_IndependentCase_DimensionalConsistency_ReturnsTypedVolumeAndMass()
    {
        var od = new KnownValue(2d, SiUnit.Meter, Refs, "src");
        var id = new KnownValue(1d, SiUnit.Meter, Refs, "src");
        var l = new KnownValue(1d, SiUnit.Meter, Refs, "src");
        var rho = new KnownValue(1d, SiUnit.KilogramPerCubicMeter, Refs, "src");

        var result = QuantityFormulas.PipeMetalMass(od, id, l, rho, Refs);

        var volume = Assert.IsType<KnownValue>(result.MetalVolume);
        var mass = Assert.IsType<KnownValue>(result.Mass);
        var expected = Math.PI / 4d * (4d - 1d) * 1d;
        Assert.Equal(expected, volume.Value, precision: 12);
        Assert.Equal(SiUnit.CubicMeter, volume.Unit);
        Assert.Equal("Q03.1.0", volume.MethodVersion);
        Assert.Equal(expected, mass.Value, precision: 12);
        Assert.Equal(SiUnit.Kilogram, mass.Unit);
        Assert.Equal("Q03.1.0", mass.MethodVersion);
    }

    [Fact]
    public void Q03_UnknownInput_ReturnsUnknowns()
    {
        var od = new UnknownValue(SiUnit.Meter, Refs, "src", "missing od", ["od:missing"]);
        var id = new KnownValue(1d, SiUnit.Meter, Refs, "src");
        var l = new KnownValue(1d, SiUnit.Meter, Refs, "src");
        var rho = new KnownValue(7850d, SiUnit.KilogramPerCubicMeter, Refs, "src");

        var result = QuantityFormulas.PipeMetalMass(od, id, l, rho, Refs);

        var unknownVolume = Assert.IsType<UnknownValue>(result.MetalVolume);
        var unknownMass = Assert.IsType<UnknownValue>(result.Mass);
        Assert.Contains("od:unknown", unknownVolume.IssueRefs);
        Assert.Contains("od:unknown", unknownMass.IssueRefs);
        Assert.Null(unknownVolume.Value);
        Assert.Null(unknownMass.Value);
    }

    [Fact]
    public void Q03_InvalidDimensionsOrDensity_FailsExplicitly()
    {
        var od = new KnownValue(1d, SiUnit.Meter, Refs, "src");
        var id = new KnownValue(1d, SiUnit.Meter, Refs, "src");
        var l = new KnownValue(1d, SiUnit.Meter, Refs, "src");
        var rho = new KnownValue(7850d, SiUnit.KilogramPerCubicMeter, Refs, "src");

        Assert.Throws<ArgumentOutOfRangeException>(() => QuantityFormulas.PipeMetalMass(od, id, l, rho, Refs));

        var badRho = new KnownValue(0d, SiUnit.KilogramPerCubicMeter, Refs, "src");
        var goodOd = new KnownValue(2d, SiUnit.Meter, Refs, "src");
        var goodId = new KnownValue(1d, SiUnit.Meter, Refs, "src");
        Assert.Throws<ArgumentOutOfRangeException>(() => QuantityFormulas.PipeMetalMass(goodOd, goodId, l, badRho, Refs));
    }
}

internal static class DecimalDoubleTestExtensions
{
    public static double ToDouble(this decimal value) => (double)value;
}
