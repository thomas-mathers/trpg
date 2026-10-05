using TRPG.Application.WorldGeneration.Generators;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.WorldGeneration.Generators;

public class DistrictConnectorGeneratorTests
{
    private static readonly DistrictType[] AllTypes =
    [
        DistrictType.Residential,
        DistrictType.CityCenter,
        DistrictType.CityEntrance,
        DistrictType.Encampment,
        DistrictType.Governmental,
        DistrictType.HolySite,
        DistrictType.Scientific,
    ];

    private readonly Guid _worldId = Guid.NewGuid();
    private readonly Guid _cityId = Guid.NewGuid();

    [Fact]
    public void Generate_ReturnsNothing_ForALoneDistrict()
    {
        // Arrange
        var districts = Districts(DistrictType.CityCenter);

        // Act
        var result = DistrictConnectorGenerator.Generate(districts, _worldId);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void Generate_PutsTheEntranceOnTheCenterSouthEdge()
    {
        // Arrange
        var districts = Districts(DistrictType.CityCenter, DistrictType.CityEntrance);

        // Act
        var result = DistrictConnectorGenerator.Generate(districts, _worldId);

        // Assert
        Assert.Equal(CompassDirection.South, ExitFrom(result, districts[0], districts[1]));
        Assert.Equal(CompassDirection.North, ExitFrom(result, districts[1], districts[0]));
    }

    [Fact]
    public void Generate_GivesEveryConnectorAReverseOnTheOppositeEdge()
    {
        // Arrange
        var districts = Districts(AllTypes);

        // Act
        var result = DistrictConnectorGenerator.Generate(districts, _worldId);

        // Assert
        Assert.All(
            result,
            connector =>
            {
                var reverse = result.Single(candidate =>
                    candidate.OriginLocationId == connector.DestinationLocationId
                    && candidate.DestinationLocationId == connector.OriginLocationId
                );
                Assert.Equal(Opposite(connector.Direction!.Value), reverse.Direction);
            }
        );
    }

    [Fact]
    public void Generate_UsesEachEdgeOfADistrictAtMostOnce()
    {
        // Arrange
        var districts = Districts(AllTypes);

        // Act
        var result = DistrictConnectorGenerator.Generate(districts, _worldId);

        // Assert
        Assert.All(
            districts,
            district =>
            {
                var edges = result
                    .Where(connector => connector.OriginLocationId == district.LocationId)
                    .Select(connector => connector.Direction)
                    .ToArray();
                Assert.Equal(edges.Length, edges.Distinct().Count());
            }
        );
    }

    [Fact]
    public void Generate_ConnectsEveryDistrictToTheRest()
    {
        // Arrange
        var districts = Districts(AllTypes);

        // Act
        var result = DistrictConnectorGenerator.Generate(districts, _worldId);

        // Assert
        var reached = new HashSet<Guid> { districts[0].LocationId };
        var frontier = new Queue<Guid>(reached);
        while (frontier.TryDequeue(out var current))
        {
            foreach (var connector in result.Where(c => c.OriginLocationId == current))
            {
                if (reached.Add(connector.DestinationLocationId))
                {
                    frontier.Enqueue(connector.DestinationLocationId);
                }
            }
        }
        Assert.Equal(districts.Count, reached.Count);
    }

    [Fact]
    public void Generate_AssignsTheSameDirections_WhenRunTwice()
    {
        // Arrange
        var districts = Districts(AllTypes);
        var first = DistrictConnectorGenerator.Generate(districts, _worldId);

        // Act
        var second = DistrictConnectorGenerator.Generate(districts, _worldId);

        // Assert
        Assert.Equal(
            first.Select(connector => connector.Direction),
            second.Select(connector => connector.Direction)
        );
    }

    private IReadOnlyList<District> Districts(params DistrictType[] types) =>
        types.Select(type => Builders.MakeDistrict(_cityId, type, worldId: _worldId)).ToArray();

    private static CompassDirection ExitFrom(
        IReadOnlyList<LocationConnector> connectors,
        District origin,
        District destination
    ) =>
        connectors
            .Single(candidate =>
                candidate.OriginLocationId == origin.LocationId
                && candidate.DestinationLocationId == destination.LocationId
            )
            .Direction
        ?? throw new InvalidOperationException("Direction is unset.");

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
