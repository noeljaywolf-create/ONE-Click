namespace OneClick.Core.Formulas;

public static class FormulaRegister
{
    public static IReadOnlyList<FormulaDescriptor> All { get; } =
    [
        GeometryFormulas.G01,
        GeometryFormulas.G02,
        GeometryFormulas.G03,
        GeometryFormulas.G04,
        QuantityFormulas.Q01,
    ];

    public static FormulaDescriptor Get(string id)
    {
        Guard.Required(id, nameof(id));
        return All.FirstOrDefault(f => string.Equals(f.Id, id, StringComparison.Ordinal))
            ?? throw new KeyNotFoundException($"Formula '{id}' is not registered.");
    }
}
