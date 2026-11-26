namespace OneClick.Core.Ids;

public readonly record struct ConnectionCandidateId
{
    public ConnectionCandidateId(string value)
    {
        Value = Guard.Required(value, nameof(value));
    }

    public string Value { get; }

    public static ConnectionCandidateId New() => new(Guid.NewGuid().ToString("D"));

    public override string ToString() => Value;
}
