namespace OneClick.Core.Geometry;

/// <summary>
/// Immutable 3D point with finite known coordinates in metres, in the internal
/// right-handed +Z-up frame. All components are required; unknown elevation is not
/// represented by zero - use a plan-only representation when Z is unknown.
/// </summary>
public readonly record struct Point3D
{
    public Point3D(double x, double y, double z)
    {
        X = Guard.Finite(x, nameof(x));
        Y = Guard.Finite(y, nameof(y));
        Z = Guard.Finite(z, nameof(z));
    }

    public double X { get; }
    public double Y { get; }
    public double Z { get; }

    public Point2D ToPlanPoint() => new(X, Y);

    public static Point3D operator +(Point3D point, Vector3D vector) =>
        new(point.X + vector.X, point.Y + vector.Y, point.Z + vector.Z);

    public static Point3D operator -(Point3D point, Vector3D vector) =>
        new(point.X - vector.X, point.Y - vector.Y, point.Z - vector.Z);

    public static Vector3D operator -(Point3D end, Point3D start) =>
        new(end.X - start.X, end.Y - start.Y, end.Z - start.Z);

    public override string ToString() => $"({X:R}, {Y:R}, {Z:R}) m";
}
