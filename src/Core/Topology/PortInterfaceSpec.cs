using OneClick.Core.Units;

namespace OneClick.Core.Topology;

/// <summary>
/// Lightweight interface specification for a port. Nominal size is an identifier, not actual OD/ID.
/// This is a model contract; compatibility resolution belongs in assembly/catalogue logic.
/// </summary>
public sealed record PortInterfaceSpec
{
    public PortInterfaceSpec(NominalSize nominalSize, string interfaceType, string? preparation = null)
    {
        NominalSize = nominalSize ?? throw new ArgumentNullException(nameof(nominalSize));
        InterfaceType = Guard.Required(interfaceType, nameof(interfaceType));
        Preparation = preparation is null ? null : Guard.Required(preparation, nameof(preparation));
    }

    public NominalSize NominalSize { get; }

    /// <summary>Connection family/type identifier (for example grooved, threaded, flanged).</summary>
    public string InterfaceType { get; }

    /// <summary>Optional end preparation identifier when the interface requires one.</summary>
    public string? Preparation { get; }
}
