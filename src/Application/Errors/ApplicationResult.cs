namespace OneClick.Application.Errors;

/// <summary>
/// Simple boundary result placeholder for command/query operations.
/// Implementations may return success with a value or a problem-details contract.
/// </summary>
public sealed record ApplicationResult<T>(
    bool IsSuccess,
    T? Value,
    ApplicationProblemDetails? Problem)
{
    public static ApplicationResult<T> Success(T value) => new(true, value, null);

    public static ApplicationResult<T> Failure(ApplicationProblemDetails problem) =>
        new(false, default, problem);
}
