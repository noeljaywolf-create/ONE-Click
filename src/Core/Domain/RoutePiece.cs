using OneClick.Core.Geometry;
using OneClick.Core.TaggedValues;
using OneClick.Core.Units;

namespace OneClick.Core.Domain;

/// <summary>
/// Minimal physical/route piece representation for the first build slice.
/// Represents synthetic reference-network pieces; does not infer undeclared physical parts.
/// </summary>
public sealed record RoutePiece
{
    public RoutePiece(
        string id,
        Point3D startM,
        Point3D endM,
        double startNetOffsetM,
        double endNetOffsetM,
        string group,
        IReadOnlyList<string> sourceRefs,
        string methodVersion,
        TaggedValue? startElevation = null,
        TaggedValue? endElevation = null)
    {
        Id = Guard.Required(id, nameof(id));
        StartM = startM;
        EndM = endM;
        StartNetOffsetM = Guard.Finite(startNetOffsetM, nameof(startNetOffsetM));
        EndNetOffsetM = Guard.Finite(endNetOffsetM, nameof(endNetOffsetM));
        if (StartNetOffsetM < 0d)
            throw new ArgumentOutOfRangeException(nameof(startNetOffsetM), startNetOffsetM, "Net offset must be >= 0.");
        if (EndNetOffsetM < 0d)
            throw new ArgumentOutOfRangeException(nameof(endNetOffsetM), endNetOffsetM, "Net offset must be >= 0.");
        Group = Guard.Required(group, nameof(group));
        SourceRefs = NormalizeRefs(sourceRefs);
        MethodVersion = Guard.Required(methodVersion, nameof(methodVersion));
        StartElevation = startElevation ?? new KnownValue(startM.Z, SiUnit.Meter, SourceRefs, MethodVersion);
        EndElevation = endElevation ?? new KnownValue(endM.Z, SiUnit.Meter, SourceRefs, MethodVersion);
    }

    public string Id { get; }
    public Point3D StartM { get; }
    public Point3D EndM { get; }
    public double StartNetOffsetM { get; }
    public double EndNetOffsetM { get; }
    public string Group { get; }
    public IReadOnlyList<string> SourceRefs { get; }
    public string MethodVersion { get; }

    /// <summary>Tagged elevation value; unknown is preserved as unknown, not zero.</summary>
    public TaggedValue StartElevation { get; init; }

    /// <summary>Tagged elevation value; unknown is preserved as unknown, not zero.</summary>
    public TaggedValue EndElevation { get; init; }

    public RoutePiece WithElevations(TaggedValue startElevation, TaggedValue endElevation)
    {
        ArgumentNullException.ThrowIfNull(startElevation);
        ArgumentNullException.ThrowIfNull(endElevation);
        return this with { StartElevation = startElevation, EndElevation = endElevation };
    }

    private static IReadOnlyList<string> NormalizeRefs(IReadOnlyList<string>? refs)
    {
        ArgumentNullException.ThrowIfNull(refs);
        if (refs.Count == 0) return Array.Empty<string>();
        var copy = new string[refs.Count];
        for (var i = 0; i < refs.Count; i++)
            copy[i] = Guard.Required(refs[i], $"sourceRefs[{i}]");
        return Array.AsReadOnly(copy);
    }
}
