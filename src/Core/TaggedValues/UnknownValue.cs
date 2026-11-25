using OneClick.Core.Units;

namespace OneClick.Core.TaggedValues;

public sealed record UnknownValue : TaggedValue
{
    public UnknownValue(
        SiUnit unit,
        IReadOnlyList<string> sourceRefs,
        string methodVersion,
        string reason,
        IReadOnlyList<string> issueRefs)
        : base(unit, sourceRefs, methodVersion)
    {
        Reason = Guard.Required(reason, nameof(reason));
        IssueRefs = NormalizeRefs(issueRefs, nameof(issueRefs));
    }

    /// <summary>
    /// Explicit null - unknown is never represented as zero, NaN, or an absent field.
    /// </summary>
    public double? Value => null;

    public string Reason { get; }
    public IReadOnlyList<string> IssueRefs { get; }
}