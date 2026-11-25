using OneClick.Core.TaggedValues;

namespace OneClick.Application.Commands;

/// <summary>
/// Contract for reviewing a property/claim. Review decisions are recorded as evidence; accepting an assumption
/// does not change its origin into a documented fact.
/// </summary>
public sealed record ReviewPropertyCommand(
    string PropertyId,
    ReviewState ReviewState,
    string? ReviewNotes,
    string? IssueId = null) : ProjectCommand
{
    [System.Text.Json.Serialization.JsonIgnore]
    public override string CommandType => CommandTypes.ReviewProperty;
}
