namespace OneClick.Core;

internal static class Guard
{
    public static double Finite(double value, string paramName)
    {
        if (!double.IsFinite(value))
            throw new ArgumentOutOfRangeException(paramName, value, "Engineering values must be finite.");
        return value;
    }

    public static string Required(string? value, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Value is required.", paramName);
        return value;
    }
}