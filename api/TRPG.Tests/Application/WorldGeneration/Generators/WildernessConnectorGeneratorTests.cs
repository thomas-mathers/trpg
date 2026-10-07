using TRPG.Application.WorldGeneration.Generators;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.WorldGeneration.Generators;

public class WildernessConnectorGeneratorTests
{
    [Fact]
    public void Generate_CreatesConnectorsInBothDirections()
    {
        // Arrange
        var worldId = Guid.NewGuid();
        var city = Builders.MakeCity(Guid.NewGuid(), Guid.NewGuid(), name: "Brightwater");
        var cityEntrance = Builders.MakeDistrict(city.Id, DistrictType.CityEntrance);
        var wilderness = Builders.MakeLocation(worldId);

        // Act
        var result = WildernessConnectorGenerator.Generate(city, cityEntrance, wilderness, worldId);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Contains(
            result,
            connector =>
                connector.OriginLocationId == cityEntrance.LocationId
                && connector.DestinationLocationId == wilderness.Id
        );
        Assert.Contains(
            result,
            connector =>
                connector.OriginLocationId == wilderness.Id
                && connector.DestinationLocationId == cityEntrance.LocationId
                && connector.DestinationLabel == city.Name
                && connector.Description.Contains(city.Name, StringComparison.Ordinal)
        );
    }

    [Fact]
    public void Generate_PutsTheGateOnTheEntranceSouthEdge()
    {
        // Arrange
        var worldId = Guid.NewGuid();
        var city = Builders.MakeCity(Guid.NewGuid(), Guid.NewGuid(), name: "Brightwater");
        var cityEntrance = Builders.MakeDistrict(city.Id, DistrictType.CityEntrance);
        var wilderness = Builders.MakeLocation(worldId);

        // Act
        var result = WildernessConnectorGenerator.Generate(city, cityEntrance, wilderness, worldId);

        // Assert
        var gate = result.Single(connector =>
            connector.OriginLocationId == cityEntrance.LocationId
        );
        Assert.Equal(CompassDirection.South, gate.Direction);
    }
}
