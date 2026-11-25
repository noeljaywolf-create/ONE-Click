namespace OneClick.Core.Ids;

public readonly record struct ProjectId
{
    public ProjectId(string value)
    {
        Value = Guard.Required(value, nameof(value));
    }

    public string Value { get; }

    public static ProjectId New() => new(Guid.NewGuid().ToString("D"));

    public override string ToString() => Value;
}