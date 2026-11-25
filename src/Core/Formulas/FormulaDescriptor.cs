using OneClick.Core.Units;

namespace OneClick.Core.Formulas;

/// <summary>
/// Stable formula identity and contract metadata. Method version changes when behavior
/// affecting a quantity changes; callers must not compare quantities across versions
/// as if they were the same result.
/// </summary>
public sealed record FormulaDescriptor(
    string Id,
    string MethodVersion,
    SiUnit OutputUnit,
    string Formula,
    string Applicability,
    string FailureHandling)
{
    public string Id { get; } = Guard.Required(Id, nameof(Id));
    public string MethodVersion { get; } = Guard.Required(MethodVersion, nameof(MethodVersion));
    public SiUnit OutputUnit { get; } = Enum.IsDefined(OutputUnit)
        ? OutputUnit
        : throw new ArgumentOutOfRangeException(nameof(OutputUnit), OutputUnit, "Unit must be a defined SI unit.");
    public string Formula { get; } = Guard.Required(Formula, nameof(Formula));
    public string Applicability { get; } = Guard.Required(Applicability, nameof(Applicability));
    public string FailureHandling { get; } = Guard.Required(FailureHandling, nameof(FailureHandling));
}
