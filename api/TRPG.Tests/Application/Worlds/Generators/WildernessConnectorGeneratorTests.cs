using TRPG.Application.Worlds.Generators;
using TRPG.Data.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Worlds.Generators;

public class WildernessConnectorGeneratorTests
{
    [Fact]
    public void Generate_CreatesTravelConnectorsInBothDirections()
    {
        // Arrange
        var worldId = Guid.NewGuid();
        var city = Builders.MakeCity(Guid.NewGuid(), Guid.NewGuid(), name: "Brightwater");
        var cityEntrance = Builders.MakeDistrict(city.Id, DistrictType.CityEntrance);
        var wilderness = Builders.MakeLocation(worldId);

        // Act
        var result = WildernessConnectorGenerator.Generate(city, cityEntrance, wilderness, worldId);

        // Assert
        Assert.Equal(2, result.LocationConnectors.Count);
        Assert.Equal(2, result.TravelConnectors.Count);
        Assert.Contains(
            result.LocationConnectors,
            connector =>
                connector.OriginLocationId == cityEntrance.LocationId
                && connector.DestinationLocationId == wilderness.Id
        );
        Assert.Contains(
            result.LocationConnectors,
            connector =>
                connector.OriginLocationId == wilderness.Id
                && connector.DestinationLocationId == cityEntrance.LocationId
                && connector.DestinationLabel == city.Name
                && connector.Description.Contains(city.Name, StringComparison.Ordinal)
        );
        Assert.All(
            result.TravelConnectors,
            connector =>
            {
                Assert.Equal(1, connector.TravelTimeHours);
                Assert.Contains(
                    result.LocationConnectors,
                    locationConnector => locationConnector.Id == connector.ConnectorId
                );
            }
        );
    }
}
