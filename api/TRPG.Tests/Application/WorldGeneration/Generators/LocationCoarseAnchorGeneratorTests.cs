using TRPG.Application.WorldGeneration.Generators;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.WorldGeneration.Generators;

public class LocationCoarseAnchorGeneratorTests
{
    [Fact]
    public void Generate_AnchorsACityLocation_ToItsCityEntrance()
    {
        // Arrange
        var stateId = Guid.NewGuid();
        var city = Builders.MakeCity(stateId, Guid.NewGuid());
        var cityEntrance = Builders.MakeDistrict(city.Id, DistrictType.CityEntrance);
        var shopLocation = Builders.MakeLocation(stateId: stateId, cityId: city.Id);

        // Act
        var result = LocationCoarseAnchorGenerator.Generate(
            [shopLocation],
            [cityEntrance],
            new Dictionary<Guid, Location>()
        );

        // Assert
        Assert.Equal(cityEntrance.LocationId, result.Single().CoarseAnchorLocationId);
    }

    [Fact]
    public void Generate_AnchorsTheCityEntranceLocationItself_ToItself()
    {
        // Arrange
        var city = Builders.MakeCity(Guid.NewGuid(), Guid.NewGuid());
        var cityEntrance = Builders.MakeDistrict(city.Id, DistrictType.CityEntrance);
        var cityEntranceLocation = Builders.MakeLocation(
            cityId: city.Id,
            id: cityEntrance.LocationId
        );

        // Act
        var result = LocationCoarseAnchorGenerator.Generate(
            [cityEntranceLocation],
            [cityEntrance],
            new Dictionary<Guid, Location>()
        );

        // Assert
        Assert.Equal(cityEntrance.LocationId, result.Single().CoarseAnchorLocationId);
    }

    [Fact]
    public void Generate_AnchorsAWildernessLocation_ToItself()
    {
        // Arrange
        var wilderness = Builders.MakeLocation(kind: LocationKind.Wilderness);

        // Act
        var result = LocationCoarseAnchorGenerator.Generate(
            [wilderness],
            [],
            new Dictionary<Guid, Location>()
        );

        // Assert
        Assert.Equal(wilderness.Id, result.Single().CoarseAnchorLocationId);
    }

    [Fact]
    public void Generate_AnchorsADungeonRoom_ToItsStatesWilderness()
    {
        // Arrange
        var stateId = Guid.NewGuid();
        var wilderness = Builders.MakeLocation(stateId: stateId, kind: LocationKind.Wilderness);
        var dungeonRoom = Builders.MakeLocation(
            stateId: stateId,
            roomId: Guid.NewGuid(),
            kind: LocationKind.Room
        );

        // Act
        var result = LocationCoarseAnchorGenerator.Generate(
            [dungeonRoom],
            [],
            new Dictionary<Guid, Location> { [stateId] = wilderness }
        );

        // Assert
        Assert.Equal(wilderness.Id, result.Single().CoarseAnchorLocationId);
    }
}
