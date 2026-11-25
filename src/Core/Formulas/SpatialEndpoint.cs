using OneClick.Core.Geometry;

namespace OneClick.Core.Formulas;

/// <summary>
/// Spatial point input for formulas that must distinguish known elevation from unknown elevation.
/// Unknown elevation is represented by <see cref="ElevationM"/> == null, never by zero.
/// </summary>
public readonly record struct SpatialEndpoint
{
    public SpatialEndpoint(Point2D plan, double? elevationM)
    {
        Plan = plan;
        if (elevationM.HasValue)
            Guard.Finite(elevationM.Value, nameof(elevationM));
        ElevationM = elevationM;
    }

    public Point2D Plan { get; }
    public double? ElevationM { get; }

    public bool HasKnownElevation => ElevationM.HasValue;

    public static SpatialEndpoint FromPoint3D(Point3D point) => new(point.ToPlanPoint(), point.Z);

    public Point3D RequirePoint3D()
    {
        if (!ElevationM.HasValue)
            throw new InvalidOperationException("Spatial endpoint elevation is unknown.");
        return new Point3D(Plan.X, Plan.Y, ElevationM.Value);
    }
}
