namespace OneClick.Core.Domain;

/// <summary>
/// Minimal in-memory calibration record. Stores metadata/refs only; no calibration math.
/// </summary>
public sealed record CalibrationRecord
{
    public CalibrationRecord(
        string calibrationId,
        string regionId,
        IReadOnlyList<CalibrationControlPointRecord> controlPoints,
        string? notes,
        IReadOnlyList<string> sourceRefs,
        string methodVersion)
    {
        CalibrationId = Guard.Required(calibrationId, nameof(calibrationId));
        RegionId = Guard.Required(regionId, nameof(regionId));
        ArgumentNullException.ThrowIfNull(controlPoints, nameof(controlPoints));
        var copy = new CalibrationControlPointRecord[controlPoints.Count];
        for (var i = 0; i < controlPoints.Count; i++)
            copy[i] = controlPoints[i] ?? throw new ArgumentException("Control point is required.", $"{nameof(controlPoints)}[{i}]");
        ControlPoints = Array.AsReadOnly(copy);
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes;
        SourceRefs = NormalizeIds(sourceRefs, nameof(sourceRefs));
        MethodVersion = Guard.Required(methodVersion, nameof(methodVersion));
    }

    public string CalibrationId { get; }
    public string RegionId { get; }
    public IReadOnlyList<CalibrationControlPointRecord> ControlPoints { get; }
    public string? Notes { get; }
    public IReadOnlyList<string> SourceRefs { get; }
    public string MethodVersion { get; }

    private static IReadOnlyList<string> NormalizeIds(IReadOnlyList<string>? values, string paramName)
    {
        ArgumentNullException.ThrowIfNull(values, paramName);
        if (values.Count == 0) return Array.Empty<string>();
        var copy = new string[values.Count];
        for (var i = 0; i < values.Count; i++)
            copy[i] = Guard.Required(values[i], $"{paramName}[{i}]");
        return Array.AsReadOnly(copy);
    }
}

/// <summary>
/// Immutable control point metadata record. No calibration math is performed.
/// </summary>
public sealed record CalibrationControlPointRecord(
    double SourceX,
    double SourceY,
    double ModelX,
    double ModelY,
    double? ModelZ);
