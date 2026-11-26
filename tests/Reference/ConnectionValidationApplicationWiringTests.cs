using OneClick.Application.Contracts;
using OneClick.Core.Geometry;
using OneClick.Core.Ids;
using OneClick.Core.Topology;
using OneClick.Core.Units;

namespace OneClick.Tests.Reference;

public sealed class ConnectionValidationApplicationWiringTests
{
    private static Port CreatePort(string id) =>
        new(new PortId(id), "owner-1", new Point3D(0, 0, 0), new Vector3D(1, 0, 0), new PortInterfaceSpec(new NominalSize("NPS", "2"), "grooved"));

    [Fact]
    public void ApplicationContract_ShapesConnectionValidationRequestAndResponse()
    {
        var request = new ConnectionValidationRequestDto(
            CreatePort("port-a"),
            CreatePort("port-b"),
            new ConnectionValidationMetadata());

        var result = new TopologyConnectionValidator().ValidateConnection(request.PortA, request.PortB, request.Metadata);
        var response = new ConnectionValidationResponse(result, result.Outcome == TopologyValidationOutcome.Resolved);

        Assert.True(response.IsConfirmedConnectionAllowed);
        Assert.Equal(TopologyValidationOutcome.Resolved, response.Result.Outcome);
    }

    [Fact]
    public void ApplicationContract_ServiceInterface_IsUsableAsWiringShape()
    {
        IConnectionValidationService service = new StubConnectionValidationService();
        var response = service.ValidateConnection(new ConnectionValidationRequestDto(CreatePort("a"), CreatePort("b")));

        Assert.NotNull(response.Result);
    }

    private sealed class StubConnectionValidationService : IConnectionValidationService
    {
        public ConnectionValidationResponse ValidateConnection(ConnectionValidationRequestDto request)
        {
            var result = new TopologyConnectionValidator().ValidateConnection(request.PortA, request.PortB, request.Metadata);
            return new ConnectionValidationResponse(result, result.Outcome == TopologyValidationOutcome.Resolved);
        }
    }
}
