namespace OneClick.Core.Geometry;

/// <summary>
/// Immutable 3D vector with finite components in metres, in the internal
/// right-handed +Z-up frame.
/// </summary>
public readonly record struct Vector3D
{
    public Vector3D(double x, double y, double z)
    {
        X = Guard.Finite(x, nameof(x));
        Y = Guard.Finite(y, nameof(y));
        Z = Guard.Finite(z, nameof(z));
    }

    public double X { get; }
    public double Y { get; }
    public double Z { get; }

    public double LengthSquared => Guard.Finite((X * X) + (Y * Y) + (Z * Z), nameof(LengthSquared));

    public double Length => Guard.Finite(Math.Sqrt(LengthSquared), nameof(Length));

    public static Vector3D operator +(Vector3D left, Vector3D right) =>
        new(left.X + right.X, left.Y + right.Y, left.Z + right.Z);

    public static Vector3D operator -(Vector3D left, Vector3D right) =>
        new(left.X - right.X, left.Y - right.Y, left.Z - right.Z);

    public static Vector3D operator *(Vector3D vector, double scalar) =>
        new(vector.X * scalar, vector.Y * scalar, vector.Z * scalar);

    public static Vector3D operator *(double scalar, Vector3D vector) => vector * scalar;

    public double Dot(Vector3D other) =>
        Guard.Finite((X * other.X) + (Y * other.Y) + (Z * other.Z), nameof(Dot));

    public Vector3D Cross(Vector3D other) =>
        new(
            (Y * other.Z) - (Z * other.Y),
            (Z * other.X) - (X * other.Z),
            (X * other.Y) - (Y * other.X));

    public override string ToString() => $"<{X:R}, {Y:R}, {Z:R}> m";
}
