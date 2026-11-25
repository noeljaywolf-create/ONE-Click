using OneClick.Core.Ids;
using OneClick.Core.Units;

namespace OneClick.Tests.Reference;

public sealed class UnitsAndIdsTests
{
    [Fact]
    public void UnitConventions_UseSiInternalUnits()
    {
        Assert.Equal("m", UnitConventions.SpatialUnit);
        Assert.Equal("rad", UnitConventions.AngleUnit);
        Assert.Equal("Pa", UnitConventions.PressureUnit);
        Assert.Equal("m3/s", UnitConventions.FlowUnit);
        Assert.Equal("kg", UnitConventions.MassUnit);

        Assert.Equal("m", UnitConventions.Symbol(SiUnit.Meter));
        Assert.Equal("rad", UnitConventions.Symbol(SiUnit.Radian));
        Assert.Equal("Pa", UnitConventions.Symbol(SiUnit.Pascal));
        Assert.Equal("m3/s", UnitConventions.Symbol(SiUnit.CubicMeterPerSecond));
        Assert.Equal("kg", UnitConventions.Symbol(SiUnit.Kilogram));
    }

    [Fact]
    public void CoordinateFrameConvention_IsRightHandedZUp()
    {
        var frame = CoordinateFrameConvention.Default;

        Assert.True(frame.RightHanded);
        Assert.Equal("+Z", frame.UpAxis);
        Assert.Equal(SiUnit.Meter, frame.SpatialUnit);
        Assert.Equal(SiUnit.Radian, frame.AngleUnit);
    }

    [Fact]
    public void NominalSize_IsIdentifier_NotMeasurement()
    {
        var size = new NominalSize("NPS", "1 1/2");

        Assert.Equal("NPS", size.System);
        Assert.Equal("1 1/2", size.Designation);
        Assert.Equal("NPS:1 1/2", size.ToString());
    }

    [Theory]
    [InlineData("", "1")]
    [InlineData("NPS", "")]
    [InlineData(" ", "1")]
    [InlineData("NPS", " ")]
    public void NominalSize_RequiresSystemAndDesignation(string system, string designation)
    {
        Assert.ThrowsAny<ArgumentException>(() => new NominalSize(system, designation));
    }

    [Fact]
    public void TypedIds_RejectEmptyAndExposeValue()
    {
        var project = new ProjectId("proj-1");
        var rev = new DocumentRevisionId("rev-1");

        Assert.Equal("proj-1", project.Value);
        Assert.Equal("rev-1", rev.ToString());
        Assert.Equal("rev-1", rev.Value);

        Assert.ThrowsAny<ArgumentException>(() => new ProjectId(""));
        Assert.ThrowsAny<ArgumentException>(() => new DocumentRevisionId(" "));
    }

    [Fact]
    public void TypedIds_NewCreatesStableFormattedValue()
    {
        var a = ProjectId.New();
        var b = ProjectId.New();

        Assert.NotEqual(a, b);
        Assert.True(Guid.TryParseExact(a.Value, "D", out _));
    }
}