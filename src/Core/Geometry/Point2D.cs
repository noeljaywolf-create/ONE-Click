namespace OneClick.Core.Geometry;

/// <summary>
/// Immutable plan-space point with finite known X/Y coordinates in metres.
/// Carries no elevation; it must not be used to invent a Z coordinate.
/// Unknown elevation means unknown 3D spatial length unless separately justified.
/// </summary>
public readonly record struct Point2D
{
    public Point2D(double x, double y)
    {
        X = Guard.Finite(x, nameof(x));
        Y = Guard.Finite(y, nameof(y));
    }

    public double X { get; }
    public double Y { get; }

    public override string ToString() => $"({X:R}, {Y:R}) m";
}
