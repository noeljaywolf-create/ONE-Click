namespace OneClick.Application.Validation;

/// <summary>
/// Validates command boundary inputs before command execution.
/// Structural validation must not require persistence; context hooks allow later precondition and existence checks.
/// </summary>
public interface ICommandBoundaryValidator
{
    Task<CommandValidationResult> ValidateAsync(
        CommandValidationRequest request,
        ICommandValidationContext? context = null,
        CancellationToken cancellationToken = default);
}
