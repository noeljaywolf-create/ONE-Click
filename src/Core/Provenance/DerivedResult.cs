namespace OneClick.Core.Provenance;

/// <summary>
/// Lightweight derived-result shape that binds a value to required provenance metadata.
/// </summary>
public sealed record DerivedResult<T> : IDerivedResult
{
    public DerivedResult(T value, ResultProvenance provenance)
    {
        if (value is null)
            throw new ArgumentNullException(nameof(value));
        Value = value;
        Provenance = provenance ?? throw new ArgumentNullException(nameof(provenance));
    }

    public T Value { get; }

    public ResultProvenance Provenance { get; }
}
