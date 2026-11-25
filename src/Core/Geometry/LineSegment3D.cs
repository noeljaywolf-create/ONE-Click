namespace OneClick.Core.Geometry;

/// <summary>
/// Immutable straight 3D segment between two finite known 3D points.
/// Spatial length is defined because both endpoints carry known finite Z.
/// </summary>
public sealed record LineSegment3D
{
    public LineSegment3D(Point3D start, Point3D end)
    {
        Start = start;
        End = end;
    }

    public Point3D Start { get; }
    public Point3D End { get; }

    public Vector3D Direction => End - Start;

    public double Length => Direction.Length;

    public override string ToString() => $"{Start} -> {End}";
}
