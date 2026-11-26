namespace OneClick.Core.Ids;

public readonly record struct AssemblyId
{
    public AssemblyId(string value)
    {
        Value = Guard.Required(value, nameof(value));
    }

    public string Value { get; }

    public static AssemblyId New() => new(Guid.NewGuid().ToString("D"));

    public override string ToString() => Value;
}
