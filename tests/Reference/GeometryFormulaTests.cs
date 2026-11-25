using OneClick.Core.Formulas;
using OneClick.Core.Geometry;
using OneClick.Core.TaggedValues;
using OneClick.Core.Units;

namespace OneClick.Tests.Reference;

public sealed class GeometryFormulaTests
{
    private static readonly string[] Refs = ["obs:line:1"];

    [Fact]
    public void G01_IndependentCase_Displacement345_Returns13Meters()
    {
        var start = new Point3D(0, 0, 0);
        var end = new Point3D(3, 4, 12);

        var result = GeometryFormulas.StraightSegmentLength(start, end, Refs);

        var known = Assert.IsType<KnownValue>(result);
        Assert.Equal(13d, known.Value);
        Assert.Equal(SiUnit.Meter, known.Unit);
        Assert.Equal("G01.1.0", known.MethodVersion);
        Assert.Equal(Refs, known.SourceRefs);
    }

    [Fact]
    public void G01_UnknownElevation_ReturnsUnknownSpatialLength()
    {
        var start = new SpatialEndpoint(new Point2D(0, 0), 0d);
        var end = new SpatialEndpoint(new Point2D(3, 4), null);

        var result = GeometryFormulas.StraightSegmentLength(start, end, Refs);

        var unknown = Assert.IsType<UnknownValue>(result);
        Assert.Null(unknown.Value);
        Assert.Equal(SiUnit.Meter, unknown.Unit);
        Assert.Equal("G01.1.0", unknown.MethodVersion);
        Assert.Contains("elevation:unknown:end", unknown.IssueRefs);
        Assert.Contains("unknown spatial length", unknown.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void G02_IndependentCase_3mThen4m_Returns7Meters()
    {
        var vertices = new[]
        {
            new Point3D(0, 0, 0),
            new Point3D(3, 0, 0),
            new Point3D(3, 4, 0),
        };

        var result = GeometryFormulas.PolylineLength(vertices, Refs);

        var known = Assert.IsType<KnownValue>(result);
        Assert.Equal(7d, known.Value);
        Assert.Equal(SiUnit.Meter, known.Unit);
        Assert.Equal("G02.1.0", known.MethodVersion);
    }

    [Fact]
    public void G02_IncludesIntermediateOffsets()
    {
        var vertices = new[]
        {
            new Point3D(0, 0, 0),
            new Point3D(1, 0, 0),
            new Point3D(1, 1, 0),
            new Point3D(2, 1, 0),
        };

        var result = GeometryFormulas.PolylineLength(vertices, Refs);
        var known = Assert.IsType<KnownValue>(result);
        Assert.Equal(3d, known.Value);
    }

    [Fact]
    public void G02_UnknownElevation_ReturnsUnknown()
    {
        var vertices = new[]
        {
            new SpatialEndpoint(new Point2D(0, 0), 0d),
            new SpatialEndpoint(new Point2D(3, 0), null),
            new SpatialEndpoint(new Point2D(3, 4), 0d),
        };

        var result = GeometryFormulas.PolylineLength(vertices, Refs);
        var unknown = Assert.IsType<UnknownValue>(result);
        Assert.Contains("elevation:unknown:vertex:1", unknown.IssueRefs);
    }

    [Fact]
    public void G03_IndependentCase_OrthogonalDirections_ReturnsPiOver2()
    {
        var u = new Vector3D(1, 0, 0);
        var v = new Vector3D(0, 1, 0);

        var result = GeometryFormulas.DirectionAngle(u, v, Refs);

        Assert.Equal(Math.PI / 2d, result.Value, precision: 12);
        Assert.Equal(SiUnit.Radian, result.Unit);
        Assert.Equal("G03.1.0", result.MethodVersion);
    }

    [Fact]
    public void G03_ZeroDirection_ThrowsExplicitly()
    {
        var zero = new Vector3D(0, 0, 0);
        var v = new Vector3D(0, 1, 0);

        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => GeometryFormulas.DirectionAngle(zero, v, Refs));
        Assert.Equal("u", ex.ParamName);
    }

    [Fact]
    public void G04_IndependentCase_PointAboveMidSegment_ReturnsSqrt2()
    {
        var p = new Point3D(2, 1, 0);
        var a = new Point3D(0, 0, 0);
        var b = new Point3D(1, 0, 0);

        var result = GeometryFormulas.PointSegmentClosestPoint(p, a, b, Refs);

        Assert.Equal(Math.Sqrt(2d), result.Distance.Value, precision: 12);
        Assert.Equal(new Point3D(1, 0, 0), result.ClosestPoint);
        Assert.Equal(1d, result.ParameterT);
        Assert.False(result.SegmentIsZeroLength);
        Assert.Equal(SiUnit.Meter, result.Distance.Unit);
        Assert.Equal("G04.1.0", result.Distance.MethodVersion);
    }

    [Fact]
    public void G04_ZeroLengthSegment_ReturnsPointDistance()
    {
        var p = new Point3D(3, 4, 0);
        var a = new Point3D(0, 0, 0);
        var b = new Point3D(0, 0, 0);

        var result = GeometryFormulas.PointSegmentClosestPoint(p, a, b, Refs);

        Assert.Equal(5d, result.Distance.Value);
        Assert.Equal(new Point3D(0, 0, 0), result.ClosestPoint);
        Assert.Equal(0d, result.ParameterT);
        Assert.True(result.SegmentIsZeroLength);
    }

    [Fact]
    public void FormulaRegister_ContainsG01ToG04AndQ01()
    {
        var ids = FormulaRegister.All.Select(f => f.Id).ToArray();
        Assert.Contains("G01", ids);
        Assert.Contains("G02", ids);
        Assert.Contains("G03", ids);
        Assert.Contains("G04", ids);
        Assert.Contains("Q01", ids);
        Assert.Equal("G01.1.0", FormulaRegister.Get("G01").MethodVersion);
        Assert.Equal(SiUnit.Radian, FormulaRegister.Get("G03").OutputUnit);
    }
}
