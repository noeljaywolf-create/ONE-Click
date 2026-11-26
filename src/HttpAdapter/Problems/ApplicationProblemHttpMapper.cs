using OneClick.Application.Errors;

namespace OneClick.HttpAdapter.Problems;

/// <summary>
/// Maps application problem contracts to RFC 9457-shaped HTTP response DTOs.
/// No web server or framework dependency is required.
/// </summary>
public static class ApplicationProblemHttpMapper
{
    public const string ProblemContentType = "application/problem+json";

    public static HttpProblemResponse FromProblemDetails(ApplicationProblemDetails problem)
    {
        ArgumentNullException.ThrowIfNull(problem);

        var body = new HttpProblemDetailsResponse(
            problem.Type,
            problem.Title,
            problem.Status,
            problem.Detail,
            problem.Instance,
            problem.Code,
            problem.IssueIds,
            problem.CurrentRevision?.Value);

        return new HttpProblemResponse(problem.Status, ProblemContentType, body);
    }

    public static HttpProblemResponse FromException(ApplicationProblemException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return FromProblemDetails(exception.Problem);
    }

    public static HttpProblemResponse FromException(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        if (exception is ApplicationProblemException appProblem)
        {
            return FromException(appProblem);
        }

        var fallback = new ApplicationProblemDetails(
            ApplicationProblemTypes.InternalError,
            "Unhandled application error",
            500,
            "An unexpected error occurred.",
            null,
            ApplicationProblemCodes.InternalError,
            Array.Empty<string>(),
            null);

        return FromProblemDetails(fallback);
    }
}
