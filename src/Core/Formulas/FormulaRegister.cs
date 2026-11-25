namespace OneClick.Core.Formulas;

public static class FormulaRegister
{
    public static IReadOnlyList<FormulaDescriptor> All { get; } =
    [
        GeometryFormulas.G01,
        GeometryFormulas.G02,
        GeometryFormulas.G03,
        GeometryFormulas.G04,
        GeometryFormulas.G07,
        QuantityFormulas.Q01,
        QuantityFormulas.Q02,
        QuantityFormulas.Q03,
    ];

    public static FormulaDescriptor Get(string id)
    {
        Guard.Required(id, nameof(id));
        return All.FirstOrDefault(f => string.Equals(f.Id, id, StringComparison.Ordinal))
            ?? throw new KeyNotFoundException($"Formula '{id}' is not registered.");
    }
}
