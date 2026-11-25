namespace OneClick.Application.Errors;

/// <summary>
/// Stable application problem codes for boundary errors. Values are placeholders for contract scaffolding
/// and should be extended as gates define specific failure modes.
/// </summary>
public static class ApplicationProblemCodes
{
    public const string MalformedPayload = "oneclick.malformed_payload";
    public const string Unauthorized = "oneclick.unauthorized";
    public const string Forbidden = "oneclick.forbidden";
    public const string NotFound = "oneclick.not_found";
    public const string Conflict = "oneclick.conflict";
    public const string StaleRevision = "oneclick.stale_revision";
    public const string PayloadTooLarge = "oneclick.payload_too_large";
    public const string InvalidGeometry = "oneclick.invalid_geometry";
    public const string PreconditionRequired = "oneclick.precondition_required";
}
