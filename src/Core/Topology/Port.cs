using OneClick.Core.Geometry;
using OneClick.Core.Ids;

namespace OneClick.Core.Topology;

/// <summary>
/// Immutable port model foundation. Ports define connection interfaces; nearby endpoints are
/// candidates until validated and a confirmed joint is created.
/// </summary>
public sealed record Port
{
    private const double UnitNormalTolerance = 1e-9;

    public Port(
        PortId id,
        string ownerId,
        Point3D position,
        Vector3D outwardNormal,
        PortInterfaceSpec interfaceSpec,
        double? clockingRadians = null)
    {
        Guard.Required(id.Value, nameof(id));
        Id = id;
        OwnerId = Guard.Required(ownerId, nameof(ownerId));
        Position = position;
        OutwardNormal = ValidateUnitNormal(outwardNormal, nameof(outwardNormal));
        InterfaceSpec = interfaceSpec ?? throw new ArgumentNullException(nameof(interfaceSpec));
        ClockingRadians = clockingRadians is null ? null : Guard.Finite(clockingRadians.Value, nameof(clockingRadians));
    }

    public PortId Id { get; }

    /// <summary>Stable identifier of the owning component/route-end entity.</summary>
    public string OwnerId { get; }

    public Point3D Position { get; }

    /// <summary>Unit outward normal in internal metres frame; must be finite and unit length.</summary>
    public Vector3D OutwardNormal { get; }

    public PortInterfaceSpec InterfaceSpec { get; }

    /// <summary>Optional clocking angle in radians.</summary>
    public double? ClockingRadians { get; }

    private static Vector3D ValidateUnitNormal(Vector3D normal, string paramName)
    {
        var length = normal.Length;
        if (Math.Abs(length - 1d) > UnitNormalTolerance)
        {
            throw new ArgumentOutOfRangeException(
                paramName,
                normal,
                $"Outward normal must be a unit vector within {UnitNormalTolerance:R} tolerance; length was {length:R}.");
        }

        return normal;
    }
}
