using OneClick.Application.Errors;

namespace OneClick.Application.Errors;

/// <summary>
/// Carries an <see cref="ApplicationProblemDetails"/> across the in-process application boundary.
/// Transport adapters may map this to an HTTP problem response; in-process callers may inspect the problem.
/// </summary>
public sealed class ApplicationProblemException : Exception
{
    public ApplicationProblemException(ApplicationProblemDetails problem)
        : base(problem?.Detail ?? problem?.Title ?? "Application problem.")
    {
        ArgumentNullException.ThrowIfNull(problem);
        Problem = problem;
    }

    public ApplicationProblemDetails Problem { get; }
}
