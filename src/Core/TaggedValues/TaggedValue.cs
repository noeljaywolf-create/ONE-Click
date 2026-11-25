using OneClick.Core.Units;

namespace OneClick.Core.TaggedValues;

/// <summary>
/// Closed tagged-value union. Every variant carries unit, source refs, and method version.
/// Unknown is not represented by an absent field, zero, or NaN.
/// Known describes available calculation input/output, not professional approval.
/// </summary>
public abstract record TaggedValue
{
    private protected TaggedValue(SiUnit unit, IReadOnlyList<string> sourceRefs, string methodVersion)
    {
        if (!Enum.IsDefined(unit))
            throw new ArgumentOutOfRangeException(nameof(unit), unit, "Unit must be a defined SI unit.");

        Unit = unit;
        SourceRefs = NormalizeRefs(sourceRefs, nameof(sourceRefs));
        MethodVersion = Guard.Required(methodVersion, nameof(methodVersion));
    }

    public SiUnit Unit { get; }
    public IReadOnlyList<string> SourceRefs { get; }
    public string MethodVersion { get; }

    private protected static IReadOnlyList<string> NormalizeRefs(IReadOnlyList<string>? refs, string paramName)
    {
        ArgumentNullException.ThrowIfNull(refs, paramName);
        if (refs.Count == 0)
            return Array.Empty<string>();

        var copy = new string[refs.Count];
        for (var i = 0; i < refs.Count; i++)
            copy[i] = Guard.Required(refs[i], $"{paramName}[{i}]");
        return Array.AsReadOnly(copy);
    }
}