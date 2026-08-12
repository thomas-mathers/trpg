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
        var cityCenter = Builders.MakeDistrict(Guid.NewGuid(), DistrictType.CityCenter);
        var wilderness = Builders.MakeLocation(worldId);

        // Act
        var result = WildernessConnectorGenerator.Generate(cityCenter, wilderness, worldId);

        // Assert
        Assert.Equal(2, result.LocationConnectors.Count);
        Assert.Equal(2, result.TravelConnectors.Count);
        Assert.Contains(
            result.LocationConnectors,
            connector =>
                connector.OriginLocationId == cityCenter.LocationId
                && connector.DestinationLocationId == wilderness.Id
        );
        Assert.Contains(
            result.LocationConnectors,
            connector =>
                connector.OriginLocationId == wilderness.Id
                && connector.DestinationLocationId == cityCenter.LocationId
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
