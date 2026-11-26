namespace OneClick.Core.Topology;

/// <summary>
/// Typed topology validation issue. Codes are stable identifiers for deterministic handling.
/// </summary>
public sealed record TopologyIssue
{
    public TopologyIssue(string code, string message, string? field = null)
    {
        Code = Guard.Required(code, nameof(code));
        Message = Guard.Required(message, nameof(message));
        Field = field is null ? null : Guard.Required(field, nameof(field));
    }

    public string Code { get; }

    public string Message { get; }

    public string? Field { get; }
}
