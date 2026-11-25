using OneClick.Core.Geometry;
using OneClick.Core.Provenance;
using OneClick.Core.TaggedValues;
using OneClick.Core.Units;

namespace OneClick.Core.Formulas;

public static class GeometryFormulas
{
    public static FormulaDescriptor G01 { get; } = new(
        Id: "G01",
        MethodVersion: "G01.1.0",
        OutputUnit: SiUnit.Meter,
        Formula: "L = hypot(dx,dy,dz)",
        Applicability: "Straight segment with finite known endpoints; unknown elevation -> unknown spatial length.",
        FailureHandling: "Returns UnknownValue when either endpoint elevation is unknown; rejects non-finite coordinates.");

    public static FormulaDescriptor G02 { get; } = new(
        Id: "G02",
        MethodVersion: "G02.1.0",
        OutputUnit: SiUnit.Meter,
        Formula: "sum(norm(p[i+1]-p[i]))",
        Applicability: "Ordered polyline, include intermediate offsets.",
        FailureHandling: "Returns UnknownValue when any vertex elevation is unknown; rejects non-finite coordinates.");

    public static FormulaDescriptor G03 { get; } = new(
        Id: "G03",
        MethodVersion: "G03.1.0",
        OutputUnit: SiUnit.Radian,
        Formula: "angle = atan2(norm(cross(u,v)),dot(u,v))",
        Applicability: "Nonzero unit travel directions; outward mating normals use opposite convention.",
        FailureHandling: "Rejects zero or non-finite direction vectors; rejects non-finite angle result.");

    public static FormulaDescriptor G04 { get; } = new(
        Id: "G04",
        MethodVersion: "G04.1.0",
        OutputUnit: SiUnit.Meter,
        Formula: "t=clamp(dot(p-a,b-a)/dot(b-a,b-a),0,1)",
        Applicability: "Point/segment closest point; zero-length segment -> point distance.",
        FailureHandling: "Rejects non-finite inputs; zero-length segment uses point distance.");

    public static FormulaDescriptor G07 { get; } = new(
        Id: "G07",
        MethodVersion: "G07.1.0",
        OutputUnit: SiUnit.Meter,
        Formula: "arcLength=abs(radius*sweepRadians)",
        Applicability: "Known circular arc; preserve intended sweep, not shortest arc.",
        FailureHandling: "Returns UnknownValue when radius or sweep is unknown; rejects radius<=0, non-finite sweep, or non-finite result.");

    /// <summary>G01: straight 3D segment length with explicit unknown-elevation handling.</summary>
    public static TaggedValue StraightSegmentLength(
        SpatialEndpoint start,
        SpatialEndpoint end,
        IReadOnlyList<string> sourceRefs)
    {
        ArgumentNullException.ThrowIfNull(sourceRefs);

        if (!start.HasKnownElevation || !end.HasKnownElevation)
        {
            var issues = new List<string>();
            if (!start.HasKnownElevation) issues.Add("elevation:unknown:start");
            if (!end.HasKnownElevation) issues.Add("elevation:unknown:end");
            return new UnknownValue(
                G01.OutputUnit,
                sourceRefs,
                G01.MethodVersion,
                "Unknown elevation -> unknown spatial length.",
                issues);
        }

        var a = start.RequirePoint3D();
        var b = end.RequirePoint3D();
        var dx = b.X - a.X;
        var dy = b.Y - a.Y;
        var dz = b.Z - a.Z;
        var length = Guard.Finite(Math.Sqrt((dx * dx) + (dy * dy) + (dz * dz)), "length");
        return new KnownValue(length, G01.OutputUnit, sourceRefs, G01.MethodVersion);
    }

    /// <summary>G01 convenience overload for known 3D endpoints.</summary>
    public static KnownValue StraightSegmentLength(Point3D start, Point3D end, IReadOnlyList<string> sourceRefs)
    {
        var result = StraightSegmentLength(SpatialEndpoint.FromPoint3D(start), SpatialEndpoint.FromPoint3D(end), sourceRefs);
        return (KnownValue)result;
    }

    /// <summary>G02: ordered polyline length, including intermediate offsets.</summary>
    public static TaggedValue PolylineLength(IReadOnlyList<SpatialEndpoint> vertices, IReadOnlyList<string> sourceRefs)
    {
        ArgumentNullException.ThrowIfNull(vertices);
        ArgumentNullException.ThrowIfNull(sourceRefs);

        if (vertices.Count < 2)
            return new KnownValue(0d, G02.OutputUnit, sourceRefs, G02.MethodVersion);

        var missing = new List<string>();
        for (var i = 0; i < vertices.Count; i++)
        {
            if (!vertices[i].HasKnownElevation)
                missing.Add($"elevation:unknown:vertex:{i}");
        }

        if (missing.Count > 0)
        {
            return new UnknownValue(
                G02.OutputUnit,
                sourceRefs,
                G02.MethodVersion,
                "Unknown elevation -> unknown spatial length.",
                missing);
        }

        double total = 0d;
        for (var i = 1; i < vertices.Count; i++)
        {
            var a = vertices[i - 1].RequirePoint3D();
            var b = vertices[i].RequirePoint3D();
            var dx = b.X - a.X;
            var dy = b.Y - a.Y;
            var dz = b.Z - a.Z;
            total = Guard.Finite(total + Math.Sqrt((dx * dx) + (dy * dy) + (dz * dz)), nameof(total));
        }

        return new KnownValue(total, G02.OutputUnit, sourceRefs, G02.MethodVersion);
    }

    /// <summary>G02 convenience overload for known 3D vertices.</summary>
    public static KnownValue PolylineLength(IReadOnlyList<Point3D> vertices, IReadOnlyList<string> sourceRefs)
    {
        ArgumentNullException.ThrowIfNull(vertices);
        var endpoints = new SpatialEndpoint[vertices.Count];
        for (var i = 0; i < vertices.Count; i++)
            endpoints[i] = SpatialEndpoint.FromPoint3D(vertices[i]);
        return (KnownValue)PolylineLength(endpoints, sourceRefs);
    }

    /// <summary>G03: angle between nonzero travel directions.</summary>
    public static KnownValue DirectionAngle(Vector3D u, Vector3D v, IReadOnlyList<string> sourceRefs)
    {
        ArgumentNullException.ThrowIfNull(sourceRefs);
        EnsureNonZeroDirection(u, nameof(u));
        EnsureNonZeroDirection(v, nameof(v));

        var cross = u.Cross(v);
        var crossNorm = Guard.Finite(Math.Sqrt(cross.Dot(cross)), "crossNorm");
        var dot = u.Dot(v);
        var angle = Guard.Finite(Math.Atan2(crossNorm, dot), "angle");
        return new KnownValue(angle, G03.OutputUnit, sourceRefs, G03.MethodVersion);
    }

    /// <summary>G04: closest point on segment [a,b] to point p, with distance.</summary>
    public static SegmentClosestPointResult PointSegmentClosestPoint(Point3D p, Point3D a, Point3D b, IReadOnlyList<string> sourceRefs)
    {
        ArgumentNullException.ThrowIfNull(sourceRefs);

        var ab = b - a;
        var ap = p - a;
        var denom = ab.Dot(ab);

        double t;
        if (denom == 0d)
        {
            // Zero-length segment -> point distance.
            t = 0d;
        }
        else
        {
            t = Guard.Finite(ap.Dot(ab) / denom, nameof(t));
            t = Math.Clamp(t, 0d, 1d);
        }

        var closest = new Point3D(
            a.X + (ab.X * t),
            a.Y + (ab.Y * t),
            a.Z + (ab.Z * t));

        var distance = Guard.Finite((p - closest).Length, "distance");
        var value = new KnownValue(distance, G04.OutputUnit, sourceRefs, G04.MethodVersion);
        return new SegmentClosestPointResult(value, closest, t, denom == 0d);
    }

    /// <summary>G07: circular arc length. Preserves intended sweep, not shortest arc.</summary>
    public static TaggedValue ArcLength(TaggedValue radius, TaggedValue sweepRadians, IReadOnlyList<string> sourceRefs)
    {
        ArgumentNullException.ThrowIfNull(radius);
        ArgumentNullException.ThrowIfNull(sweepRadians);
        ArgumentNullException.ThrowIfNull(sourceRefs);

        var issues = new List<string>();
        if (radius is UnknownValue)
            issues.Add("radius:unknown");
        if (sweepRadians is UnknownValue)
            issues.Add("sweep:unknown");
        if (issues.Count > 0)
        {
            return new UnknownValue(
                G07.OutputUnit,
                sourceRefs,
                G07.MethodVersion,
                "Unknown radius or sweep -> unknown arc length.",
                issues);
        }

        var r = RequireKnown(radius, SiUnit.Meter, nameof(radius));
        var sweep = RequireKnown(sweepRadians, SiUnit.Radian, nameof(sweepRadians));

        if (r <= 0d)
            throw new ArgumentOutOfRangeException(nameof(radius), r, "G07 requires radius > 0.");

        // sweep must be finite (KnownValue already guarantees finite, but be explicit about the guard contract).
        Guard.Finite(sweep, nameof(sweepRadians));

        var length = Guard.Finite(Math.Abs(r * sweep), "arcLength");
        return new KnownValue(length, G07.OutputUnit, sourceRefs, G07.MethodVersion);
    }

    /// <summary>G07 convenience overload for known numeric inputs.</summary>
    public static KnownValue ArcLength(double radiusM, double sweepRadians, IReadOnlyList<string> sourceRefs)
    {
        Guard.Finite(radiusM, nameof(radiusM));
        Guard.Finite(sweepRadians, nameof(sweepRadians));

        var result = ArcLength(
            new KnownValue(radiusM, SiUnit.Meter, sourceRefs, G07.MethodVersion),
            new KnownValue(sweepRadians, SiUnit.Radian, sourceRefs, G07.MethodVersion),
            sourceRefs);
        return (KnownValue)result;
    }

    private static double RequireKnown(TaggedValue value, SiUnit expectedUnit, string paramName)
    {
        if (value is not KnownValue known)
            throw new ArgumentException($"Expected known {expectedUnit} value.", paramName);
        if (known.Unit != expectedUnit)
            throw new ArgumentException($"Expected unit {expectedUnit} but got {known.Unit}.", paramName);
        return known.Value;
    }

    private static void EnsureNonZeroDirection(Vector3D vector, string paramName)
    {
        // Components already finite by Vector3D invariant. Reject zero vector explicitly.
        if (vector.X == 0d && vector.Y == 0d && vector.Z == 0d)
            throw new ArgumentOutOfRangeException(paramName, vector, "Direction vector must be nonzero.");
    }

    // --- Provenance-carrying overloads (G01-G04, G07) ---

    public static DerivedResult<TaggedValue> StraightSegmentLength(
        SpatialEndpoint start,
        SpatialEndpoint end,
        IReadOnlyList<string> sourceRefs,
        IReadOnlyList<InputReference> inputs,
        IReadOnlyList<QuantityContributionId> contributionIds)
    {
        var value = StraightSegmentLength(start, end, sourceRefs);
        return FormulaProvenance.WrapTagged(value, inputs, contributionIds);
    }

    public static DerivedResult<KnownValue> StraightSegmentLength(
        Point3D start,
        Point3D end,
        IReadOnlyList<string> sourceRefs,
        IReadOnlyList<InputReference> inputs,
        IReadOnlyList<QuantityContributionId> contributionIds)
    {
        var value = StraightSegmentLength(start, end, sourceRefs);
        return FormulaProvenance.WrapTagged(value, inputs, contributionIds);
    }

    public static DerivedResult<TaggedValue> PolylineLength(
        IReadOnlyList<SpatialEndpoint> vertices,
        IReadOnlyList<string> sourceRefs,
        IReadOnlyList<InputReference> inputs,
        IReadOnlyList<QuantityContributionId> contributionIds)
    {
        var value = PolylineLength(vertices, sourceRefs);
        return FormulaProvenance.WrapTagged(value, inputs, contributionIds);
    }

    public static DerivedResult<KnownValue> PolylineLength(
        IReadOnlyList<Point3D> vertices,
        IReadOnlyList<string> sourceRefs,
        IReadOnlyList<InputReference> inputs,
        IReadOnlyList<QuantityContributionId> contributionIds)
    {
        var value = PolylineLength(vertices, sourceRefs);
        return FormulaProvenance.WrapTagged(value, inputs, contributionIds);
    }

    public static DerivedResult<KnownValue> DirectionAngle(
        Vector3D u,
        Vector3D v,
        IReadOnlyList<string> sourceRefs,
        IReadOnlyList<InputReference> inputs,
        IReadOnlyList<QuantityContributionId> contributionIds)
    {
        var value = DirectionAngle(u, v, sourceRefs);
        return FormulaProvenance.WrapTagged(value, inputs, contributionIds);
    }

    public static DerivedResult<SegmentClosestPointResult> PointSegmentClosestPoint(
        Point3D p,
        Point3D a,
        Point3D b,
        IReadOnlyList<string> sourceRefs,
        IReadOnlyList<InputReference> inputs,
        IReadOnlyList<QuantityContributionId> contributionIds)
    {
        var value = PointSegmentClosestPoint(p, a, b, sourceRefs);
        return FormulaProvenance.Wrap(value, value.Distance.MethodVersion, inputs, value.Distance.SourceRefs, contributionIds);
    }

    public static DerivedResult<TaggedValue> ArcLength(
        TaggedValue radius,
        TaggedValue sweepRadians,
        IReadOnlyList<string> sourceRefs,
        IReadOnlyList<InputReference> inputs,
        IReadOnlyList<QuantityContributionId> contributionIds)
    {
        var value = ArcLength(radius, sweepRadians, sourceRefs);
        return FormulaProvenance.WrapTagged(value, inputs, contributionIds);
    }

    public static DerivedResult<KnownValue> ArcLength(
        double radiusM,
        double sweepRadians,
        IReadOnlyList<string> sourceRefs,
        IReadOnlyList<InputReference> inputs,
        IReadOnlyList<QuantityContributionId> contributionIds)
    {
        var value = ArcLength(radiusM, sweepRadians, sourceRefs);
        return FormulaProvenance.WrapTagged(value, inputs, contributionIds);
    }
}

public sealed record SegmentClosestPointResult(
    KnownValue Distance,
    Point3D ClosestPoint,
    double ParameterT,
    bool SegmentIsZeroLength)
{
    public KnownValue Distance { get; } = Distance ?? throw new ArgumentNullException(nameof(Distance));
    public double ParameterT { get; } = Guard.Finite(ParameterT, nameof(ParameterT));
}
