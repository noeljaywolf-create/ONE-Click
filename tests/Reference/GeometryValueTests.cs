using OneClick.Core.Geometry;
using OneClick.Core.Units;

namespace OneClick.Tests.Reference;

public sealed class GeometryValueTests
{
    [Fact]
    public void Point3D_StoresFiniteCoordinates_InMetres()
    {
        var point = new Point3D(1.5, -2.25, 3.0);

        Assert.Equal(1.5, point.X);
        Assert.Equal(-2.25, point.Y);
        Assert.Equal(3.0, point.Z);
        Assert.EndsWith(" m", point.ToString());
        Assert.Contains("1.5", point.ToString());
        Assert.Contains("-2.25", point.ToString());
    }

    [Theory]
    [InlineData(double.NaN, 0, 0, "x")]
    [InlineData(double.PositiveInfinity, 0, 0, "x")]
    [InlineData(double.NegativeInfinity, 0, 0, "x")]
    [InlineData(0, double.NaN, 0, "y")]
    [InlineData(0, double.PositiveInfinity, 0, "y")]
    [InlineData(0, 0, double.NaN, "z")]
    [InlineData(0, 0, double.NegativeInfinity, "z")]
    public void Point3D_RejectsNonFiniteComponents(double x, double y, double z, string paramName)
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new Point3D(x, y, z));
        Assert.Equal(paramName, ex.ParamName);
    }

    [Fact]
    public void Point2D_StoresFinitePlanCoordinates_WithoutInventingElevation()
    {
        var point = new Point2D(10.0, 20.0);

        Assert.Equal(10.0, point.X);
        Assert.Equal(20.0, point.Y);

        // Plan-only point carries no Z. Unknown elevation must not be represented as 0.
        var members = typeof(Point2D).GetProperties().Select(p => p.Name).ToArray();
        Assert.DoesNotContain("Z", members);
    }

    [Theory]
    [InlineData(double.NaN, 0, "x")]
    [InlineData(0, double.PositiveInfinity, "y")]
    [InlineData(double.NegativeInfinity, 0, "x")]
    public void Point2D_RejectsNonFiniteComponents(double x, double y, string paramName)
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new Point2D(x, y));
        Assert.Equal(paramName, ex.ParamName);
    }

    [Fact]
    public void Vector3D_StoresFiniteComponents_AndComputesLength()
    {
        var vector = new Vector3D(3, 4, 0);

        Assert.Equal(5d, vector.Length);
        Assert.Equal(25d, vector.LengthSquared);
    }

    [Theory]
    [InlineData(double.NaN, 0, 0)]
    [InlineData(0, double.PositiveInfinity, 0)]
    [InlineData(0, 0, double.NegativeInfinity)]
    public void Vector3D_RejectsNonFiniteComponents(double x, double y, double z)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Vector3D(x, y, z));
    }

    [Fact]
    public void Vector3D_Operations_RejectNonFiniteResults()
    {
        var max = new Vector3D(double.MaxValue, 0, 0);

        Assert.Throws<ArgumentOutOfRangeException>(() => max + max);
        Assert.Throws<ArgumentOutOfRangeException>(() => max * 2d);
        Assert.Throws<ArgumentOutOfRangeException>(() => max.Dot(max));
    }

    [Fact]
    public void Point3D_Subtraction_ReturnsFiniteVector()
    {
        var start = new Point3D(1, 2, 3);
        var end = new Point3D(4, 6, 3);

        var delta = end - start;

        Assert.Equal(new Vector3D(3, 4, 0), delta);
        Assert.Equal(5d, delta.Length);
    }

    [Fact]
    public void LineSegment3D_HasFiniteKnownLength()
    {
        var segment = new LineSegment3D(new Point3D(0, 0, 0), new Point3D(0, 0, 10));

        Assert.Equal(10d, segment.Length);
        Assert.Equal(new Vector3D(0, 0, 10), segment.Direction);
    }

    [Fact]
    public void Polyline3D_CopiesVertices_AndComputesLength()
    {
        var vertices = new List<Point3D>
        {
            new(0, 0, 0),
            new(3, 4, 0),
            new(3, 4, 10),
        };

        var polyline = new Polyline3D(vertices);
        vertices[0] = new Point3D(999, 999, 999);

        Assert.Equal(15d, polyline.Length);
        Assert.Equal(new Point3D(0, 0, 0), polyline.Vertices[0]);
        Assert.Equal(3, polyline.Vertices.Count);
    }

    [Fact]
    public void Polyline3D_RejectsNullVertices()
    {
        Assert.Throws<ArgumentNullException>(() => new Polyline3D(null!));
    }

    [Fact]
    public void Polyline3D_ShortVertexLists_HaveZeroLength()
    {
        Assert.Equal(0d, new Polyline3D([]).Length);
        Assert.Equal(0d, new Polyline3D([new Point3D(1, 2, 3)]).Length);
    }

    [Fact]
    public void UnknownElevation_CannotBeRepresentedAsPoint3D()
    {
        // There is no Point3D constructor or factory that accepts an unknown Z.
        // A plan-only point must be represented as Point2D; 3D length is unavailable.
        var plan = new Point2D(5, 6);
        var known3D = new Point3D(plan.X, plan.Y, 7);

        Assert.Equal(7d, known3D.Z);
        Assert.Equal(plan, known3D.ToPlanPoint());
    }

    [Fact]
    public void DrawingAxisConventions_AreExplicit()
    {
        var plan = DrawingAxisConvention.PlanYUp;
        var raster = DrawingAxisConvention.RasterYDown;

        Assert.True(plan.YUp);
        Assert.False(plan.YDown);
        Assert.False(raster.YUp);
        Assert.True(raster.YDown);
        Assert.Contains("+Y up", plan.Description);
        Assert.Contains("+Y down", raster.Description);
    }

    [Fact]
    public void CoordinateFrameConvention_RemainsRightHandedZUp()
    {
        var frame = CoordinateFrameConvention.Default;

        Assert.True(frame.RightHanded);
        Assert.Equal("+Z", frame.UpAxis);
        Assert.Equal(SiUnit.Meter, frame.SpatialUnit);
        Assert.Equal(SiUnit.Radian, frame.AngleUnit);
    }
}
