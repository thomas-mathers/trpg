using TRPG.Application.Configuration;
using TRPG.Application.WorldGeneration.Generators;
using TRPG.Domain.Models;

namespace TRPG.Tests.Application.WorldGeneration.Generators;

public class CityTravelConnectorGeneratorTests
{
    private static readonly Guid WorldId = Guid.NewGuid();
    private static readonly CityTravelOptions Options = new()
    {
        BuildingDistance = 5,
        DistrictDistance = 15,
    };

    [Fact]
    public void Generate_AddsMeasuredDistrictConnectorsInBothDirections()
    {
        var cityId = Guid.NewGuid();
        var first = District(cityId);
        var second = District(cityId);
        var forward = Connector(first.LocationId, second.LocationId);
        var reverse = Connector(second.LocationId, first.LocationId);

        var travelConnectors = CityTravelConnectorGenerator.Generate(
            WorldId,
            [first, second],
            [],
            [],
            [forward, reverse],
            Options
        );

        Assert.Equal(2, travelConnectors.Count);
        Assert.All(
            travelConnectors,
            connector => Assert.Equal(Options.DistrictDistance, connector.Distance)
        );
    }

    [Fact]
    public void Generate_AddsMeasuredBuildingEntranceConnectorsInBothDirections()
    {
        var district = District(Guid.NewGuid());
        var building = new Building { WorldId = WorldId, ExteriorLocationId = district.LocationId };
        var room = new Room
        {
            WorldId = WorldId,
            BuildingId = building.Id,
            LocationId = Guid.NewGuid(),
        };
        var entry = Connector(district.LocationId, room.LocationId);
        var exit = Connector(room.LocationId, district.LocationId);

        var travelConnectors = CityTravelConnectorGenerator.Generate(
            WorldId,
            [district],
            [building],
            [room],
            [entry, exit],
            Options
        );

        Assert.Equal(2, travelConnectors.Count);
        Assert.All(
            travelConnectors,
            connector => Assert.Equal(Options.BuildingDistance, connector.Distance)
        );
    }

    private static District District(Guid cityId) =>
        new()
        {
            WorldId = WorldId,
            CityId = cityId,
            LocationId = Guid.NewGuid(),
        };

    private static LocationConnector Connector(Guid originLocationId, Guid destinationLocationId) =>
        new()
        {
            WorldId = WorldId,
            OriginLocationId = originLocationId,
            DestinationLocationId = destinationLocationId,
            DestinationLabel = "Destination",
        };
}
