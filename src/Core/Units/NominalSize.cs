namespace OneClick.Core.Units;

/// <summary>
/// Nominal size is an identifier, not actual OD/ID. Catalogue values may themselves
/// be nominal dimensions; that qualification must be preserved by callers.
/// </summary>
public sealed record NominalSize
{
    public NominalSize(string system, string designation)
    {
        if (string.IsNullOrWhiteSpace(system))
            throw new ArgumentException("Nominal size system is required.", nameof(system));
        if (string.IsNullOrWhiteSpace(designation))
            throw new ArgumentException("Nominal size designation is required.", nameof(designation));

        System = system.Trim();
        Designation = designation.Trim();
    }

    public string System { get; }
    public string Designation { get; }

    public override string ToString() => $"{System}:{Designation}";
}