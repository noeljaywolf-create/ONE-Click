using OneClick.Application.Contracts;
using OneClick.Application.Errors;
using OneClick.HttpAdapter.Problems;

namespace OneClick.Tests.Integration;

public sealed class ApplicationProblemHttpMapperTests
{
    [Fact]
    public void Maps_ProblemDetails_To_Rfc9457_Response_With_Extensions()
    {
        var problem = new ApplicationProblemDetails(
            ApplicationProblemTypes.StaleRevision,
            "Stale project revision",
            412,
            "Expected revision does not match current project revision.",
            "urn:oneclick:instance:cmd-1",
            ApplicationProblemCodes.StaleRevision,
            ["issue-7"],
            new ProjectRevision(8));

        var response = ApplicationProblemHttpMapper.FromProblemDetails(problem);

        Assert.Equal(412, response.StatusCode);
        Assert.Equal("application/problem+json", response.ContentType);
        Assert.Equal(ApplicationProblemTypes.StaleRevision, response.Body.Type);
        Assert.Equal("Stale project revision", response.Body.Title);
        Assert.Equal(412, response.Body.Status);
        Assert.Equal("Expected revision does not match current project revision.", response.Body.Detail);
        Assert.Equal("urn:oneclick:instance:cmd-1", response.Body.Instance);
        Assert.Equal(ApplicationProblemCodes.StaleRevision, response.Body.Code);
        Assert.Equal(["issue-7"], response.Body.IssueIds);
        Assert.Equal(8, response.Body.CurrentRevision);
    }

    [Fact]
    public void Maps_ApplicationProblemException_To_Response()
    {
        var problem = new ApplicationProblemDetails(
            ApplicationProblemTypes.Conflict,
            "Command conflict",
            409,
            "Replay conflict.",
            "urn:oneclick:validation:commandId",
            ApplicationProblemCodes.Conflict,
            [],
            new ProjectRevision(3));

        var response = ApplicationProblemHttpMapper.FromException(new ApplicationProblemException(problem));

        Assert.Equal(409, response.StatusCode);
        Assert.Equal("application/problem+json", response.ContentType);
        Assert.Equal(ApplicationProblemCodes.Conflict, response.Body.Code);
        Assert.Equal(3, response.Body.CurrentRevision);
    }

    [Fact]
    public void Maps_Unknown_Exception_To_Internal_Error_Response()
    {
        var response = ApplicationProblemHttpMapper.FromException(new InvalidOperationException("boom"));

        Assert.Equal(500, response.StatusCode);
        Assert.Equal("application/problem+json", response.ContentType);
        Assert.Equal(ApplicationProblemTypes.InternalError, response.Body.Type);
        Assert.Equal(ApplicationProblemCodes.InternalError, response.Body.Code);
        Assert.Equal(500, response.Body.Status);
        Assert.Null(response.Body.CurrentRevision);
        Assert.Empty(response.Body.IssueIds);
    }
}
