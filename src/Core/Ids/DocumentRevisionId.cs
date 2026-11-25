namespace OneClick.Core.Ids;

public readonly record struct DocumentRevisionId
{
    public DocumentRevisionId(string value)
    {
        Value = Guard.Required(value, nameof(value));
    }

    public string Value { get; }

    public static DocumentRevisionId New() => new(Guid.NewGuid().ToString("D"));

    public override string ToString() => Value;
}