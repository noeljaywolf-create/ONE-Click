using OneClick.Core.Geometry;
using OneClick.Core.Provenance;
using OneClick.Core.TaggedValues;
using OneClick.Core.Units;

namespace OneClick.Core.Formulas;

public sealed record AssemblyResidualResult(
    KnownValue PositionResidual,
    KnownValue AngularResidual,
    TaggedValue ClockingResidual,
    string Limitation)
{
    public KnownValue PositionResidual { get; } = PositionResidual ?? throw new ArgumentNullException(nameof(PositionResidual));
    public KnownValue AngularResidual { get; } = AngularResidual ?? throw new ArgumentNullException(nameof(AngularResidual));
    public TaggedValue ClockingResidual { get; } = ClockingResidual ?? throw new ArgumentNullException(nameof(ClockingResidual));
    public string Limitation { get; } = Guard.Required(Limitation, nameof(Limitation));
}

public static class AssemblyFormulas
{
    private const double UnitVectorTolerance = 1e-9;

    public static FormulaDescriptor A03 { get; } = new(
        Id: "A03",
        MethodVersion: "A03.1.0",
        OutputUnit: SiUnit.Meter,
        Formula: "positionResidual=norm(pA-pB); angularResidual=acos(clamp(-dot(nA,nB),-1,1))",
        Applicability: "Finite positions and unit outward normals. Keyed interfaces require explicit clocking residual input; clocking is not faked.",
        FailureHandling: "Rejects non-finite/non-unit normals or non-finite positions; rejects invalid clocking unit/value. Returns typed residuals with explicit limitation when clocking is unknown.");

    /// <summary>
    /// A03: position and angular residual between two interface frames.
    /// Normals are unit outward normals. Clocking is explicit: pass a known radian value
    /// when evaluated, or an UnknownValue when clocking is keyed/not evaluated. This method
    /// does not fake clocking.
    /// </summary>
    public static AssemblyResidualResult InterfaceResidual(
        Point3D pA,
        Point3D pB,
        Vector3D nA,
        Vector3D nB,
        TaggedValue clockingResidualRad,
        IReadOnlyList<string> sourceRefs)
    {
        ArgumentNullException.ThrowIfNull(clockingResidualRad);
        ArgumentNullException.ThrowIfNull(sourceRefs);

        EnsureUnitNormal(nA, nameof(nA));
        EnsureUnitNormal(nB, nameof(nB));

        var delta = pA - pB;
        var positionResidual = Guard.Finite(delta.Length, "positionResidual");

        var dot = Guard.Finite(nA.Dot(nB), "normalDot");
        var angularArg = Math.Clamp(-dot, -1d, 1d);
        var angularResidual = Guard.Finite(Math.Acos(angularArg), "angularResidual");

        var clocking = NormalizeClocking(clockingResidualRad);

        var limitation = clocking is UnknownValue
            ? "Clocking residual is explicit input; keyed-interface clocking was not evaluated. Angular residual uses outward normals only."
            : "Clocking residual is explicit input. Angular residual uses outward normals only; product fit limits/conventions are external data.";

        return new AssemblyResidualResult(
            new KnownValue(positionResidual, SiUnit.Meter, sourceRefs, A03.MethodVersion),
            new KnownValue(angularResidual, SiUnit.Radian, sourceRefs, A03.MethodVersion),
            clocking,
            limitation);
    }

    public static DerivedResult<AssemblyResidualResult> InterfaceResidual(
        Point3D pA,
        Point3D pB,
        Vector3D nA,
        Vector3D nB,
        TaggedValue clockingResidualRad,
        IReadOnlyList<string> sourceRefs,
        IReadOnlyList<InputReference> inputs,
        IReadOnlyList<QuantityContributionId> contributionIds)
    {
        var value = InterfaceResidual(pA, pB, nA, nB, clockingResidualRad, sourceRefs);
        return FormulaProvenance.Wrap(value, value.PositionResidual.MethodVersion, inputs, value.PositionResidual.SourceRefs, contributionIds);
    }

    private static void EnsureUnitNormal(Vector3D normal, string paramName)
    {
        // Vector3D components are finite by invariant; guard unit length explicitly.
        var length = normal.Length;
        if (Math.Abs(length - 1d) > UnitVectorTolerance)
        {
            throw new ArgumentOutOfRangeException(
                paramName,
                length,
                $"A03 requires a unit normal (|n|-1 within {UnitVectorTolerance:R}).");
        }
    }

    private static TaggedValue NormalizeClocking(TaggedValue clockingResidualRad)
    {
        if (clockingResidualRad is UnknownValue unknown)
        {
            if (unknown.Unit != SiUnit.Radian)
                throw new ArgumentException("A03 clocking residual unknown must use radian unit.", nameof(clockingResidualRad));
            return unknown;
        }

        if (clockingResidualRad is not KnownValue known)
            throw new ArgumentException("Clocking residual must be a known or unknown tagged value.", nameof(clockingResidualRad));

        if (known.Unit != SiUnit.Radian)
            throw new ArgumentException($"A03 clocking residual must be in radians, got {known.Unit}.", nameof(clockingResidualRad));
        if (known.Value < 0d)
            throw new ArgumentOutOfRangeException(nameof(clockingResidualRad), known.Value, "A03 clocking residual must be >= 0.");

        // Preserve the explicit known input as-is (it carries its own source/method metadata).
        return known;
    }
}
