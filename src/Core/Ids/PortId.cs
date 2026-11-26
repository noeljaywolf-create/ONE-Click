namespace OneClick.Core.Ids;

public readonly record struct PortId
{
    public PortId(string value)
    {
        Value = Guard.Required(value, nameof(value));
    }

    public string Value { get; }

    public static PortId New() => new(Guid.NewGuid().ToString("D"));

    public override string ToString() => Value;
}
