namespace OneClick.Application.Errors;

/// <summary>
/// Stable URN problem type identifiers. These are application-defined URNs, not public standards URLs.
/// </summary>
public static class ApplicationProblemTypes
{
    public const string MalformedPayload = "urn:oneclick:problem:malformed-payload";
    public const string Unauthorized = "urn:oneclick:problem:unauthorized";
    public const string Forbidden = "urn:oneclick:problem:forbidden";
    public const string NotFound = "urn:oneclick:problem:not-found";
    public const string Conflict = "urn:oneclick:problem:conflict";
    public const string StaleRevision = "urn:oneclick:problem:stale-revision";
    public const string PayloadTooLarge = "urn:oneclick:problem:payload-too-large";
    public const string InvalidGeometry = "urn:oneclick:problem:invalid-geometry";
    public const string PreconditionRequired = "urn:oneclick:problem:precondition-required";
}
