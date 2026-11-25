namespace OneClick.Core.Units;

/// <summary>
/// Internal engineering conventions. These are invariants of the Core representation,
/// not display or import preferences.
/// </summary>
public static class UnitConventions
{
    public const string SpatialUnit = "m";
    public const string AngleUnit = "rad";
    public const string PressureUnit = "Pa";
    public const string FlowUnit = "m3/s";
    public const string MassUnit = "kg";

    public static string Symbol(SiUnit unit) => unit switch
    {
        SiUnit.Meter => SpatialUnit,
        SiUnit.Radian => AngleUnit,
        SiUnit.Pascal => PressureUnit,
        SiUnit.CubicMeterPerSecond => FlowUnit,
        SiUnit.Kilogram => MassUnit,
        _ => throw new ArgumentOutOfRangeException(nameof(unit), unit, "Unsupported SI unit."),
    };
}