namespace OneClick.Application.Commands;

/// <summary>
/// Contract for setting calibration on a document region. Calibration controls must be sufficient and nondegenerate;
/// validation is performed by the application/core boundary.
/// </summary>
public sealed record SetCalibrationCommand(
    string RegionId,
    IReadOnlyList<CalibrationControlPoint> ControlPoints,
    string? CalibrationNotes) : ProjectCommand
{
    [System.Text.Json.Serialization.JsonIgnore]
    public override string CommandType => CommandTypes.SetCalibration;
}

/// <summary>
/// Calibration control point contract. Coordinates are expressed in their declared coordinate spaces;
/// unit and convention enforcement belongs to validation.
/// </summary>
public sealed record CalibrationControlPoint(
    double SourceX,
    double SourceY,
    double ModelX,
    double ModelY,
    double? ModelZ);
