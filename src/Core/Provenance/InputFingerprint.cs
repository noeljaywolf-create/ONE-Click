namespace OneClick.Core.Provenance;

/// <summary>
/// Opaque fingerprint for an input state. Callers provide the fingerprint value; this type
/// preserves it as an immutable, validated value object.
/// </summary>
public readonly record struct InputFingerprint
{
    public InputFingerprint(string value)
    {
        Value = Guard.Required(value, nameof(value));
    }

    public string Value { get; }

    public override string ToString() => Value;
}
