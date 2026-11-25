using OneClick.Core.Units;

namespace OneClick.Core.TaggedValues;

public sealed record ProvisionalValue : TaggedValue
{
    public ProvisionalValue(
        double value,
        SiUnit unit,
        IReadOnlyList<string> sourceRefs,
        string methodVersion,
        IReadOnlyList<string> assumptionRefs,
        IReadOnlyList<string> issueRefs)
        : base(unit, sourceRefs, methodVersion)
    {
        Value = Guard.Finite(value, nameof(value));
        AssumptionRefs = NormalizeRefs(assumptionRefs, nameof(assumptionRefs));
        IssueRefs = NormalizeRefs(issueRefs, nameof(issueRefs));
    }

    public double Value { get; }
    public IReadOnlyList<string> AssumptionRefs { get; }
    public IReadOnlyList<string> IssueRefs { get; }
}