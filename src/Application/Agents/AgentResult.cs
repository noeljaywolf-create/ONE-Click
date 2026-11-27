namespace OneClick.Application.Agents;

/// <summary>
/// Typed result envelope returned by local agent execution.
/// Carries issues/warnings/provenance refs so callers can inspect deterministic outputs.
/// </summary>
/// <typeparam name="TOutput">Typed output payload.</typeparam>
public sealed record AgentResult<TOutput>(
    bool IsSuccess,
    TOutput? Output,
    IReadOnlyList<string> Issues,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<string> ProvenanceRefs,
    string? ErrorCode = null,
    string? ErrorDetail = null)
{
    public IReadOnlyList<string> Issues { get; } = Normalize(Issues, nameof(Issues));
    public IReadOnlyList<string> Warnings { get; } = Normalize(Warnings, nameof(Warnings));
    public IReadOnlyList<string> ProvenanceRefs { get; } = Normalize(ProvenanceRefs, nameof(ProvenanceRefs));

    public static AgentResult<TOutput> Success(
        TOutput output,
        IReadOnlyList<string>? issues = null,
        IReadOnlyList<string>? warnings = null,
        IReadOnlyList<string>? provenanceRefs = null) =>
        new(true, output, issues ?? [], warnings ?? [], provenanceRefs ?? []);

    public static AgentResult<TOutput> Failure(
        string errorCode,
        string errorDetail,
        IReadOnlyList<string>? issues = null,
        IReadOnlyList<string>? warnings = null,
        IReadOnlyList<string>? provenanceRefs = null)
    {
        if (string.IsNullOrWhiteSpace(errorCode))
            throw new ArgumentException("Error code is required.", nameof(errorCode));
        if (string.IsNullOrWhiteSpace(errorDetail))
            throw new ArgumentException("Error detail is required.", nameof(errorDetail));

        return new(false, default, issues ?? [], warnings ?? [], provenanceRefs ?? [], errorCode, errorDetail);
    }

    private static IReadOnlyList<string> Normalize(IReadOnlyList<string>? values, string paramName)
    {
        if (values is null || values.Count == 0)
            return Array.Empty<string>();

        var copy = new string[values.Count];
        for (var i = 0; i < values.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(values[i]))
                throw new ArgumentException("Value is required.", $"{paramName}[{i}]");
            copy[i] = values[i];
        }

        return Array.AsReadOnly(copy);
    }
}
