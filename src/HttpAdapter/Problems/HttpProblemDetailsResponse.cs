using System.Text.Json.Serialization;
using OneClick.Application.Contracts;

namespace OneClick.HttpAdapter.Problems;

/// <summary>
/// RFC 9457-shaped problem details response DTO with OneClick project extensions.
/// Transport-agnostic DTO; an HTTP framework can serialize this as application/problem+json.
/// </summary>
public sealed record HttpProblemDetailsResponse(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("status")] int Status,
    [property: JsonPropertyName("detail")] string? Detail,
    [property: JsonPropertyName("instance")] string? Instance,
    [property: JsonPropertyName("code")] string Code,
    [property: JsonPropertyName("issueIds")] IReadOnlyList<string> IssueIds,
    [property: JsonPropertyName("currentRevision")] long? CurrentRevision);

/// <summary>
/// Minimal HTTP response envelope for problem mapping without requiring a web server.
/// </summary>
public sealed record HttpProblemResponse(
    [property: JsonPropertyName("statusCode")] int StatusCode,
    [property: JsonPropertyName("contentType")] string ContentType,
    [property: JsonPropertyName("body")] HttpProblemDetailsResponse Body);
