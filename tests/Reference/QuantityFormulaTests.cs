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
}

internal static class DecimalDoubleTestExtensions
{
    public static double ToDouble(this decimal value) => (double)value;
}
