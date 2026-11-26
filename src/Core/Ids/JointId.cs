namespace OneClick.Core.Ids;

public readonly record struct JointId
{
    public JointId(string value)
    {
        Value = Guard.Required(value, nameof(value));
    }

    public string Value { get; }

    public static JointId New() => new(Guid.NewGuid().ToString("D"));

    public override string ToString() => Value;
}
