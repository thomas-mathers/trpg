using TRPG.Application.WorldGeneration.Generators;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.WorldGeneration.Generators;

public class PatrolStopNodeAssignerTests
{
    private readonly Guid _worldId = Guid.NewGuid();

    [Fact]
    public void Assign_PicksTheRoadJunctionNearestTheDistrictCenter()
    {
        // Arrange
        var district = Builders.MakeLocation(_worldId, width: 100, depth: 100);
        var port = Builders.MakeTravelNode(district.Id, 0, 50);
        var nearCenter = Builders.MakeTravelNode(district.Id, 45, 50);
        var farJunction = Builders.MakeTravelNode(district.Id, 90, 90);
        var stub = Builders.MakeTravelNode(district.Id, 50, 50);
        var step = Step(district.Id);

        // Act
        PatrolStopNodeAssigner.Assign(
            [step],
            [port, nearCenter, farJunction, stub],
            [
                Road(district.Id, port, nearCenter),
                Road(district.Id, nearCenter, farJunction),
                Road(district.Id, farJunction, stub),
            ],
            new Dictionary<Guid, Location> { [district.Id] = district }
        );

        // Assert
        Assert.Equal(nearCenter.Id, step.TravelNodeId);
    }

    [Fact]
    public void Assign_GivesEveryStepAtTheSameLocationTheSameNode()
    {
        // Arrange
        var district = Builders.MakeLocation(_worldId, width: 100, depth: 100);
        var first = Builders.MakeTravelNode(district.Id, 10, 10);
        var second = Builders.MakeTravelNode(district.Id, 20, 20);
        var steps = new[] { Step(district.Id), Step(district.Id) };

        // Act
        PatrolStopNodeAssigner.Assign(
            steps,
            [first, second],
            [Road(district.Id, first, second)],
            new Dictionary<Guid, Location> { [district.Id] = district }
        );

        // Assert
        Assert.Single(steps.Select(step => step.TravelNodeId).Distinct());
    }

    private RouteStep Step(Guid locationId) =>
        new()
        {
            WorldId = _worldId,
            LocationId = locationId,
            SequenceIndex = 0,
            DwellHours = 0,
        };

    private static PointConnector Road(Guid locationId, TravelNode from, TravelNode to) =>
        Builders.MakePointConnector(locationId, from.Id, to.Id, 1, bidirectional: true);
}
