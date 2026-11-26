using OneClick.Core.Domain;
using OneClick.Core.TaggedValues;

namespace OneClick.Desktop.ViewModels.Geometry;

/// <summary>
/// Display model for a single route/piece in the geometry plan view.
/// Wraps domain data for binding; does not mutate engineering state.
/// </summary>
public sealed class GeometryPieceViewModel
{
    public GeometryPieceViewModel(RoutePiece piece)
    {
        ArgumentNullException.ThrowIfNull(piece);
        Piece = piece;
    }

    public RoutePiece Piece { get; }

    public string Id => Piece.Id;
    public string Group => Piece.Group;

    public double StartX => Piece.StartM.X;
    public double StartY => Piece.StartM.Y;
    public double StartZ => Piece.StartM.Z;
    public double EndX => Piece.EndM.X;
    public double EndY => Piece.EndM.Y;
    public double EndZ => Piece.EndM.Z;

    public TaggedValue StartElevation => Piece.StartElevation;
    public TaggedValue EndElevation => Piece.EndElevation;

    public bool HasUnknownElevation =>
        StartElevation is UnknownValue || EndElevation is UnknownValue;

    public bool HasProvisionalElevation =>
        StartElevation is ProvisionalValue || EndElevation is ProvisionalValue;

    public string ElevationStatus
    {
        get
        {
            if (HasUnknownElevation) return "Unknown Z";
            if (HasProvisionalElevation) return "Provisional Z";
            return "Known Z";
        }
    }

    public string ElevationStatusDetail
    {
        get
        {
            if (HasUnknownElevation) return "Unknown Z (unresolved, not zero)";
            if (HasProvisionalElevation) return "Provisional Z (assumed, not confirmed)";
            return "Known Z";
        }
    }

    public string LabelText
    {
        get
        {
            if (HasUnknownElevation) return $"{Id} [Z unknown]";
            if (HasProvisionalElevation) return $"{Id} [Z provisional]";
            return Id;
        }
    }

    public string DisplayLabel => $"{Id} ({Group})";

    public string ToolTip =>
        $"{Id} [{Group}]\n" +
        $"Elevation status: {ElevationStatusDetail}\n" +
        $"Start (X,Y,Z): ({StartX:0.###}, {StartY:0.###}, {FormatZ(StartZ, StartElevation)}) m\n" +
        $"End (X,Y,Z): ({EndX:0.###}, {EndY:0.###}, {FormatZ(EndZ, EndElevation)}) m\n" +
        $"Start elevation: {DescribeElevation(StartElevation)}\n" +
        $"End elevation: {DescribeElevation(EndElevation)}";

    private static string FormatZ(double z, TaggedValue elevation)
    {
        return elevation is UnknownValue ? "unknown" : $"{z:0.###}";
    }

    private static string DescribeElevation(TaggedValue value)
    {
        return value switch
        {
            KnownValue known => $"{known.Value:0.###} {known.Unit} (known)",
            ProvisionalValue provisional => $"{provisional.Value:0.###} {provisional.Unit} (provisional, not confirmed)",
            UnknownValue unknown => $"UNKNOWN - unresolved, not zero ({unknown.Reason})",
            _ => value.GetType().Name
        };
    }
}
