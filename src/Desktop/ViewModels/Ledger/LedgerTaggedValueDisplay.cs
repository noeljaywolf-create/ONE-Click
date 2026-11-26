using System.Globalization;
using OneClick.Core.TaggedValues;

namespace OneClick.Desktop.ViewModels.Ledger;

/// <summary>
/// UI display model for a tagged value. Unknown is explicitly labeled and never rendered as zero.
/// </summary>
public sealed class LedgerTaggedValueDisplay
{
    private LedgerTaggedValueDisplay(
        string status,
        string displayText,
        string detailText,
        bool isKnown,
        bool isProvisional,
        bool isUnknown,
        double? numericValue,
        string unit)
    {
        Status = status;
        DisplayText = displayText;
        DetailText = detailText;
        IsKnown = isKnown;
        IsProvisional = isProvisional;
        IsUnknown = isUnknown;
        NumericValue = numericValue;
        Unit = unit;
    }

    public string Status { get; }
    public string DisplayText { get; }
    public string DetailText { get; }
    public bool IsKnown { get; }
    public bool IsProvisional { get; }
    public bool IsUnknown { get; }
    public double? NumericValue { get; }
    public string Unit { get; }

    public static LedgerTaggedValueDisplay FromTaggedValue(TaggedValue? value, string valueLabel = "Value")
    {
        if (value is null)
        {
            return new LedgerTaggedValueDisplay(
                status: "Unknown",
                displayText: "Unknown",
                detailText: $"{valueLabel}: unknown (no value provided).",
                isKnown: false,
                isProvisional: false,
                isUnknown: true,
                numericValue: null,
                unit: string.Empty);
        }

        var unit = value.Unit.ToString();

        return value switch
        {
            KnownValue known => new LedgerTaggedValueDisplay(
                status: "Known",
                displayText: $"{known.Value.ToString("0.###", CultureInfo.InvariantCulture)} {unit}",
                detailText: $"{valueLabel}: known {known.Value.ToString("0.###", CultureInfo.InvariantCulture)} {unit}.",
                isKnown: true,
                isProvisional: false,
                isUnknown: false,
                numericValue: known.Value,
                unit: unit),

            ProvisionalValue provisional => new LedgerTaggedValueDisplay(
                status: "Provisional",
                displayText: $"Provisional: {provisional.Value.ToString("0.###", CultureInfo.InvariantCulture)} {unit}",
                detailText: $"{valueLabel}: provisional {provisional.Value.ToString("0.###", CultureInfo.InvariantCulture)} {unit}. Assumptions: {FormatRefs(provisional.AssumptionRefs)}.",
                isKnown: false,
                isProvisional: true,
                isUnknown: false,
                numericValue: provisional.Value,
                unit: unit),

            UnknownValue unknown => new LedgerTaggedValueDisplay(
                status: "Unknown",
                displayText: $"Unknown ({unit})",
                detailText: $"{valueLabel}: unknown. Reason: {unknown.Reason} Issues: {FormatRefs(unknown.IssueRefs)}.",
                isKnown: false,
                isProvisional: false,
                isUnknown: true,
                numericValue: null,
                unit: unit),

            _ => new LedgerTaggedValueDisplay(
                status: "Unknown",
                displayText: $"Unknown ({unit})",
                detailText: $"{valueLabel}: unsupported tagged value type '{value.GetType().Name}'; treated as unknown for display.",
                isKnown: false,
                isProvisional: false,
                isUnknown: true,
                numericValue: null,
                unit: unit),
        };
    }

    private static string FormatRefs(IReadOnlyList<string> refs)
        => refs.Count == 0 ? "none" : string.Join(", ", refs);

    public override string ToString() => DisplayText;
}
