namespace OneClick.Core.Units;

/// <summary>
/// Internal coordinate frame convention: right-handed, +Z upward.
/// +X/+Y are defined by project grids at the project level.
/// </summary>
public sealed record CoordinateFrameConvention
{
    public static CoordinateFrameConvention Default { get; } = new();

    public bool RightHanded => true;
    public string UpAxis => "+Z";
    public SiUnit SpatialUnit => SiUnit.Meter;
    public SiUnit AngleUnit => SiUnit.Radian;

    private CoordinateFrameConvention()
    {
    }
}