using OneClick.Core.Units;

namespace OneClick.Core.TaggedValues;

public sealed record KnownValue : TaggedValue
{
    public KnownValue(
        double value,
        SiUnit unit,
        IReadOnlyList<string> sourceRefs,
        string methodVersion)
        : base(unit, sourceRefs, methodVersion)
    {
        Value = Guard.Finite(value, nameof(value));
    }

    public double Value { get; }
}