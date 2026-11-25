namespace OneClick.Core.Provenance;

public readonly record struct QuantityContributionId
{
    public QuantityContributionId(string value)
    {
        Value = Guard.Required(value, nameof(value));
    }

    public string Value { get; }

    public static QuantityContributionId New() => new(Guid.NewGuid().ToString("D"));

    public override string ToString() => Value;
}
