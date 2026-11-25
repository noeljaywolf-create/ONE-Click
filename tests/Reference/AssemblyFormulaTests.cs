using OneClick.Core.Formulas;
using OneClick.Core.Geometry;
using OneClick.Core.TaggedValues;
using OneClick.Core.Units;

namespace OneClick.Tests.Reference;

public sealed class AssemblyFormulaTests
{
    private static readonly string[] Refs = ["obs:asm:1"];

    [Fact]
    public void A03_IndependentCase_OpposedNormals_ReturnZeroResiduals()
    {
        var clocking = new UnknownValue(SiUnit.Radian, Refs, "src", "keyed clocking not evaluated", ["clocking:not-evaluated"]);

        var result = AssemblyFormulas.InterfaceResidual(
            new Point3D(0, 0, 0),
            new Point3D(0, 0, 0),
            new Vector3D(0, 0, 1),
            new Vector3D(0, 0, -1),
            clocking,
            Refs);

        Assert.Equal(0d, result.PositionResidual.Value, precision: 12);
        Assert.Equal(SiUnit.Meter, result.PositionResidual.Unit);
        Assert.Equal("A03.1.0", result.PositionResidual.MethodVersion);
        Assert.Equal(0d, result.AngularResidual.Value, precision: 12);
        Assert.Equal(SiUnit.Radian, result.AngularResidual.Unit);
        Assert.Equal("A03.1.0", result.AngularResidual.MethodVersion);
        var unknownClocking = Assert.IsType<UnknownValue>(result.ClockingResidual);
        Assert.Contains("clocking:not-evaluated", unknownClocking.IssueRefs);
        Assert.Contains("not evaluated", result.Limitation, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void A03_PositionAndAngularResiduals_AreComputedFromExplicitInputs()
    {
        var clocking = new KnownValue(0.1d, SiUnit.Radian, Refs, "src");

        var result = AssemblyFormulas.InterfaceResidual(
            new Point3D(1, 2, 3),
            new Point3D(4, 6, 3),
            new Vector3D(1, 0, 0),
            new Vector3D(0, 1, 0),
            clocking,
            Refs);

        Assert.Equal(5d, result.PositionResidual.Value, precision: 12);
        Assert.Equal(Math.PI / 2d, result.AngularResidual.Value, precision: 12);
        var knownClocking = Assert.IsType<KnownValue>(result.ClockingResidual);
        Assert.Equal(0.1d, knownClocking.Value, precision: 12);
        Assert.Contains("explicit input", result.Limitation, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void A03_KeyedInterfaceClocking_IsExplicitAndNotFaked()
    {
        var clocking = new UnknownValue(SiUnit.Radian, Refs, "src", "keyed interface requires clocking", ["clocking:keyed"]);

        var result = AssemblyFormulas.InterfaceResidual(
            new Point3D(0, 0, 0),
            new Point3D(0, 0, 0),
            new Vector3D(0, 0, 1),
            new Vector3D(0, 0, -1),
            clocking,
            Refs);

        var unknownClocking = Assert.IsType<UnknownValue>(result.ClockingResidual);
        Assert.Equal(SiUnit.Radian, unknownClocking.Unit);
        Assert.Contains("clocking:keyed", unknownClocking.IssueRefs);
        Assert.DoesNotContain("0", result.ClockingResidual.ToString() ?? string.Empty); // sanity: not turned into known zero
    }

    [Fact]
    public void A03_NonUnitOrInvalidNormals_FailExplicitly()
    {
        var clocking = new UnknownValue(SiUnit.Radian, Refs, "src", "n/a", ["clocking:na"]);

        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => AssemblyFormulas.InterfaceResidual(
            new Point3D(0, 0, 0),
            new Point3D(0, 0, 0),
            new Vector3D(2, 0, 0),
            new Vector3D(0, 0, -1),
            clocking,
            Refs));
        Assert.Equal("nA", ex.ParamName);

        var zeroNormal = new Vector3D(0, 0, 0);
        var exZero = Assert.Throws<ArgumentOutOfRangeException>(() => AssemblyFormulas.InterfaceResidual(
            new Point3D(0, 0, 0),
            new Point3D(0, 0, 0),
            zeroNormal,
            new Vector3D(0, 0, -1),
            clocking,
            Refs));
        Assert.Equal("nA", exZero.ParamName);
    }

    [Fact]
    public void A03_InvalidClocking_FailsExplicitly()
    {
        var badUnit = new KnownValue(0d, SiUnit.Meter, Refs, "src");
        Assert.Throws<ArgumentException>(() => AssemblyFormulas.InterfaceResidual(
            new Point3D(0, 0, 0),
            new Point3D(0, 0, 0),
            new Vector3D(0, 0, 1),
            new Vector3D(0, 0, -1),
            badUnit,
            Refs));

        var negative = new KnownValue(-0.1d, SiUnit.Radian, Refs, "src");
        Assert.Throws<ArgumentOutOfRangeException>(() => AssemblyFormulas.InterfaceResidual(
            new Point3D(0, 0, 0),
            new Point3D(0, 0, 0),
            new Vector3D(0, 0, 1),
            new Vector3D(0, 0, -1),
            negative,
            Refs));

        Assert.Throws<ArgumentNullException>(() => AssemblyFormulas.InterfaceResidual(
            new Point3D(0, 0, 0),
            new Point3D(0, 0, 0),
            new Vector3D(0, 0, 1),
            new Vector3D(0, 0, -1),
            null!,
            Refs));
    }
}
