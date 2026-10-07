using TRPG.Application.WorldGeneration.Generators;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.WorldGeneration.Generators;

public class LocationHopCycleTests
{
    private readonly Guid _locationA = Guid.NewGuid();
    private readonly Guid _locationB = Guid.NewGuid();
    private readonly Guid _locationC = Guid.NewGuid();

    [Fact]
    public void Build_ReturnsOutAndBackConnectors_WhenTwoLocationsAreAdjacent()
    {
        // Arrange
        var outbound = Builders.MakeLocationConnector(_locationA, _locationB);
        var inbound = Builders.MakeLocationConnector(_locationB, _locationA);

        // Act
        var cycle = LocationHopCycle.Build([outbound, inbound], [_locationA, _locationB]);

        // Assert
        Assert.Equal([outbound.Id, inbound.Id], cycle.Select(connector => connector.Id));
    }

    [Fact]
    public void Build_RoutesThroughIntermediateLocations_WhenLocationsAreNotAdjacent()
    {
        // Arrange
        var aToB = Builders.MakeLocationConnector(_locationA, _locationB);
        var bToC = Builders.MakeLocationConnector(_locationB, _locationC);
        var cToB = Builders.MakeLocationConnector(_locationC, _locationB);
        var bToA = Builders.MakeLocationConnector(_locationB, _locationA);

        // Act
        var cycle = LocationHopCycle.Build([aToB, bToC, cToB, bToA], [_locationA, _locationC]);

        // Assert
        Assert.Equal([aToB.Id, bToC.Id, cToB.Id, bToA.Id], cycle.Select(connector => connector.Id));
    }

    [Fact]
    public void Build_PrefersTheShorterRoute_WhenTwoRoutesExist()
    {
        // Arrange
        var direct = Builders.MakeLocationConnector(_locationA, _locationC);
        var viaFirstHop = Builders.MakeLocationConnector(_locationA, _locationB);
        var viaSecondHop = Builders.MakeLocationConnector(_locationB, _locationC);
        var back = Builders.MakeLocationConnector(_locationC, _locationA);

        // Act
        var cycle = LocationHopCycle.Build(
            [direct, viaFirstHop, viaSecondHop, back],
            [_locationA, _locationC]
        );

        // Assert
        Assert.Equal([direct.Id, back.Id], cycle.Select(connector => connector.Id));
    }

    [Fact]
    public void Build_ReturnsNoConnectors_WhenLocationsAreDisconnected()
    {
        // Arrange
        var unrelated = Builders.MakeLocationConnector(_locationA, _locationB);

        // Act
        var cycle = LocationHopCycle.Build([unrelated], [_locationA, _locationC]);

        // Assert
        Assert.Empty(cycle);
    }
}
