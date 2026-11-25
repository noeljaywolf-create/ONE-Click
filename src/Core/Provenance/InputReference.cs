namespace OneClick.Core.Provenance;

/// <summary>
/// Versioned reference to an input used by a derived result. Supports the input fingerprint/ref
/// concept: <see cref="Version"/> carries a stable version token or content fingerprint as
/// provided by the caller.
/// </summary>
public sealed record InputReference
{
    public InputReference(string kind, string id, string version)
    {
        Kind = Guard.Required(kind, nameof(kind));
        Id = Guard.Required(id, nameof(id));
        Version = Guard.Required(version, nameof(version));
    }

    public InputReference(string kind, string id, InputFingerprint fingerprint)
        : this(kind, id, fingerprint.Value)
    {
    }

    /// <summary>Input kind, such as project, document-revision, catalogue-release, or policy.</summary>
    public string Kind { get; }

    /// <summary>Stable identifier of the input within its kind.</summary>
    public string Id { get; }

    /// <summary>Version token or fingerprint identifying the exact input content/state.</summary>
    public string Version { get; }
}
