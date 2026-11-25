using OneClick.Application.Errors;

namespace OneClick.Application.Validation;

/// <summary>
/// Result of command boundary validation. A failed result carries problem details that can be
/// mapped by the command service or transport adapter.
/// </summary>
public sealed record CommandValidationResult(
    bool IsValid,
    IReadOnlyList<ApplicationProblemDetails> Problems)
{
    public static CommandValidationResult Success() =>
        new(true, Array.Empty<ApplicationProblemDetails>());

    public static CommandValidationResult Failure(params ApplicationProblemDetails[] problems) =>
        new(false, problems);

    public static CommandValidationResult Failure(IReadOnlyList<ApplicationProblemDetails> problems) =>
        new(false, problems);
}
