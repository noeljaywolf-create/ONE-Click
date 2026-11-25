namespace OneClick.Core.Units;

/// <summary>
/// Explicit axis convention for drawing/raster coordinates. This is distinct from the
/// internal engineering frame; callers must not assume drawing axes match internal +Z-up space.
/// </summary>
public sealed record DrawingAxisConvention
{
    public static DrawingAxisConvention PlanYUp { get; } = new("+X right, +Y up", yUp: true);
    public static DrawingAxisConvention RasterYDown { get; } = new("+X right, +Y down", yUp: false);

    private DrawingAxisConvention(string description, bool yUp)
    {
        Description = Guard.Required(description, nameof(description));
        YUp = yUp;
    }

    public string Description { get; }
    public bool YUp { get; }
    public bool YDown => !YUp;
}
