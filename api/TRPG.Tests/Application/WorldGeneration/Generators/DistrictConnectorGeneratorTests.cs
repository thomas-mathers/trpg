using TRPG.Application.WorldGeneration.Generators;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.WorldGeneration.Generators;

public class DistrictConnectorGeneratorTests
{
    private readonly Guid _worldId = Guid.NewGuid();
    private readonly Guid _cityId = Guid.NewGuid();

    [Fact]
    public void Generate_ReturnsTwoConnectors_PerOtherDistrict()
    {
        // Arrange
        var cityCenter = Builders.MakeDistrict(_cityId, DistrictType.CityCenter, worldId: _worldId);
        var residential = Builders.MakeDistrict(
            _cityId,
            DistrictType.Residential,
            worldId: _worldId
        );

        // Act
        var result = DistrictConnectorGenerator.Generate(cityCenter, [residential], _worldId);

        // Assert
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void Generate_ConnectsEachOtherDistrictToAndFromCityCenter()
    {
        // Arrange
        var cityCenter = Builders.MakeDistrict(_cityId, DistrictType.CityCenter, worldId: _worldId);
        var residential = Builders.MakeDistrict(
            _cityId,
            DistrictType.Residential,
            worldId: _worldId
        );

        // Act
        var result = DistrictConnectorGenerator.Generate(cityCenter, [residential], _worldId);

        // Assert
        Assert.Contains(
            result,
            c =>
                c.OriginLocationId == residential.LocationId
                && c.DestinationLocationId == cityCenter.LocationId
        );
        Assert.Contains(
            result,
            c =>
                c.OriginLocationId == cityCenter.LocationId
                && c.DestinationLocationId == residential.LocationId
        );
    }

    [Fact]
    public void Generate_ReturnsEmpty_WhenNoOtherDistricts()
    {
        // Arrange
        var cityCenter = Builders.MakeDistrict(_cityId, DistrictType.CityCenter, worldId: _worldId);

        // Act
        var result = DistrictConnectorGenerator.Generate(cityCenter, [], _worldId);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void Generate_PutsTheEntranceOnTheCenterSouthEdge()
    {
        // Arrange
        var cityCenter = Builders.MakeDistrict(_cityId, DistrictType.CityCenter, worldId: _worldId);
        var entrance = Builders.MakeDistrict(_cityId, DistrictType.CityEntrance, worldId: _worldId);

        // Act
        var result = DistrictConnectorGenerator.Generate(cityCenter, [entrance], _worldId);

        // Assert
        Assert.Equal(CompassDirection.South, ExitFrom(result, cityCenter, entrance));
        Assert.Equal(CompassDirection.North, ExitFrom(result, entrance, cityCenter));
    }

    [Fact]
    public void Generate_SpreadsOtherDistrictsOverNorthEastAndWest()
    {
        // Arrange
        var cityCenter = Builders.MakeDistrict(_cityId, DistrictType.CityCenter, worldId: _worldId);
        var others = new[]
        {
            DistrictType.Residential,
            DistrictType.Scientific,
            DistrictType.Governmental,
            DistrictType.HolySite,
            DistrictType.Encampment,
        }
            .Select(type => Builders.MakeDistrict(_cityId, type, worldId: _worldId))
            .ToArray();

        // Act
        var result = DistrictConnectorGenerator.Generate(cityCenter, others, _worldId);

        // Assert
        var directions = others
            .Select(district => ExitFrom(result, cityCenter, district))
            .ToHashSet();
        Assert.Equal(
            new HashSet<CompassDirection>
            {
                CompassDirection.North,
                CompassDirection.East,
                CompassDirection.West,
            },
            directions
        );
    }

    [Fact]
    public void Generate_GivesEveryReverseConnectorTheOppositeDirection()
    {
        // Arrange
        var cityCenter = Builders.MakeDistrict(_cityId, DistrictType.CityCenter, worldId: _worldId);
        var others = new[]
        {
            DistrictType.CityEntrance,
            DistrictType.Residential,
            DistrictType.Scientific,
            DistrictType.Governmental,
        }
            .Select(type => Builders.MakeDistrict(_cityId, type, worldId: _worldId))
            .ToArray();

        // Act
        var result = DistrictConnectorGenerator.Generate(cityCenter, others, _worldId);

        // Assert
        Assert.All(
            others,
            district =>
                Assert.Equal(
                    Opposite(ExitFrom(result, cityCenter, district)),
                    ExitFrom(result, district, cityCenter)
                )
        );
    }

    [Fact]
    public void Generate_AssignsTheSameDirections_WhenRunTwice()
    {
        // Arrange
        var cityCenter = Builders.MakeDistrict(_cityId, DistrictType.CityCenter, worldId: _worldId);
        var others = new[] { DistrictType.Residential, DistrictType.Scientific }
            .Select(type => Builders.MakeDistrict(_cityId, type, worldId: _worldId))
            .ToArray();
        var first = DistrictConnectorGenerator.Generate(cityCenter, others, _worldId);

        // Act
        var second = DistrictConnectorGenerator.Generate(cityCenter, others, _worldId);

        // Assert
        Assert.Equal(
            first.Select(connector => connector.Direction),
            second.Select(connector => connector.Direction)
        );
    }

    private static CompassDirection ExitFrom(
        IReadOnlyList<LocationConnector> connectors,
        District origin,
        District destination
    )
    {
        var connector = connectors.Single(candidate =>
            candidate.OriginLocationId == origin.LocationId
            && candidate.DestinationLocationId == destination.LocationId
        );

        return connector.Direction ?? throw new InvalidOperationException("Direction is unset.");
    }

    private static CompassDirection Opposite(CompassDirection direction) =>
        direction switch
        {
            CompassDirection.North => CompassDirection.South,
            CompassDirection.South => CompassDirection.North,
            CompassDirection.East => CompassDirection.West,
            CompassDirection.West => CompassDirection.East,
            _ => throw new ArgumentOutOfRangeException(nameof(direction)),
        };
}
