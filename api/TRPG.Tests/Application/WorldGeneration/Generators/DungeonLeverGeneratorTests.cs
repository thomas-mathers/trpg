using TRPG.Application.WorldGeneration.Generators;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.WorldGeneration.Generators;

public class DungeonLeverGeneratorTests
{
    private readonly Guid _worldId = Guid.NewGuid();
    private readonly Guid _entranceLocationId = Guid.NewGuid();
    private readonly Guid _backDoorLocationId = Guid.NewGuid();
    private readonly Guid _bossLocationId = Guid.NewGuid();
    private readonly Guid _landingLocationId = Guid.NewGuid();

    [Fact]
    public void BuildMandatoryShortcutLever_LocksTheEntranceToBackDoorConnector()
    {
        // Arrange
        var entryConnector = Builders.MakeLocationConnector(
            _entranceLocationId,
            _backDoorLocationId,
            _worldId
        );

        // Act
        var result = DungeonLeverGenerator.BuildMandatoryShortcutLever(
            _worldId,
            _bossLocationId,
            _entranceLocationId,
            _backDoorLocationId,
            [entryConnector]
        );

        // Assert
        var door = Assert.Single(result.DoorConnectors);
        Assert.Equal(entryConnector.Id, door.ConnectorId);
        Assert.True(door.IsLocked);
    }

    [Fact]
    public void BuildMandatoryShortcutLever_PlacesTheLeverInTheBossRoom_LinkedToTheDoor()
    {
        // Arrange
        var entryConnector = Builders.MakeLocationConnector(
            _entranceLocationId,
            _backDoorLocationId,
            _worldId
        );

        // Act
        var result = DungeonLeverGenerator.BuildMandatoryShortcutLever(
            _worldId,
            _bossLocationId,
            _entranceLocationId,
            _backDoorLocationId,
            [entryConnector]
        );

        // Assert
        var lever = Assert.Single(result.Levers);
        Assert.Equal(_bossLocationId, lever.LocationId);
        var doorConnectorLever = Assert.Single(result.DoorConnectorLevers);
        Assert.Equal(lever.Id, doorConnectorLever.LeverId);
        Assert.Equal(result.DoorConnectors[0].Id, doorConnectorLever.DoorConnectorId);
    }

    [Fact]
    public void BuildCanonicalGate_LocksTheLandingToBossConnector()
    {
        // Arrange
        var input = MakeGateInput();

        // Act
        var result = DungeonLeverGenerator.BuildCanonicalGate(
            _worldId,
            _landingLocationId,
            _bossLocationId,
            input.Connectors,
            input.Placements
        );

        // Assert
        var door = Assert.Single(result.DoorConnectors);
        Assert.Equal(input.GatedConnector.Id, door.ConnectorId);
        Assert.True(door.IsLocked);
    }

    [Fact]
    public void BuildCanonicalGate_PlacesOneLeverPerPresentRoute_AtEachRoutesLastRoom()
    {
        // Arrange
        var input = MakeGateInput();

        // Act
        var result = DungeonLeverGenerator.BuildCanonicalGate(
            _worldId,
            _landingLocationId,
            _bossLocationId,
            input.Connectors,
            input.Placements
        );

        // Assert
        Assert.Equal(2, result.Levers.Count);
        Assert.Contains(result.Levers, lever => lever.LocationId == input.LongLastRoom.LocationId);
        Assert.Contains(result.Levers, lever => lever.LocationId == input.ShortLastRoom.LocationId);
        Assert.All(
            result.DoorConnectorLevers,
            doorConnectorLever =>
                Assert.Equal(result.DoorConnectors[0].Id, doorConnectorLever.DoorConnectorId)
        );
    }

    [Fact]
    public void BuildCanonicalGate_PlacesAThirdLever_WhenAThirdRouteIsPresent()
    {
        // Arrange
        var input = MakeGateInput();
        var thirdLastRoom = new Room
        {
            BuildingId = Guid.NewGuid(),
            LocationId = Guid.NewGuid(),
            Name = "Third Last",
            WorldId = _worldId,
        };
        var placements = input
            .Placements.Append(
                new DungeonRoomPlacement(
                    thirdLastRoom,
                    RoomRole.Passage,
                    DepthFromEntrance: 4,
                    FloorNumber: 0,
                    RouteKind: DungeonRouteKind.Third,
                    IsDeadEnd: false
                )
            )
            .ToArray();

        // Act
        var result = DungeonLeverGenerator.BuildCanonicalGate(
            _worldId,
            _landingLocationId,
            _bossLocationId,
            input.Connectors,
            placements
        );

        // Assert
        Assert.Equal(3, result.Levers.Count);
        Assert.Contains(result.Levers, lever => lever.LocationId == thirdLastRoom.LocationId);
    }

    private sealed record GateInput(
        Room LongLastRoom,
        Room ShortLastRoom,
        LocationConnector GatedConnector,
        IReadOnlyList<LocationConnector> Connectors,
        IReadOnlyList<DungeonRoomPlacement> Placements
    );

    private GateInput MakeGateInput()
    {
        var longLastRoom = new Room
        {
            BuildingId = Guid.NewGuid(),
            LocationId = Guid.NewGuid(),
            Name = "Long Last",
            WorldId = _worldId,
        };
        var shortLastRoom = new Room
        {
            BuildingId = Guid.NewGuid(),
            LocationId = Guid.NewGuid(),
            Name = "Short Last",
            WorldId = _worldId,
        };
        var gatedConnector = Builders.MakeLocationConnector(
            _landingLocationId,
            _bossLocationId,
            _worldId
        );
        var placements = new[]
        {
            new DungeonRoomPlacement(
                longLastRoom,
                RoomRole.Passage,
                DepthFromEntrance: 3,
                FloorNumber: 0,
                RouteKind: DungeonRouteKind.Long,
                IsDeadEnd: false
            ),
            new DungeonRoomPlacement(
                shortLastRoom,
                RoomRole.Passage,
                DepthFromEntrance: 2,
                FloorNumber: 0,
                RouteKind: DungeonRouteKind.Short,
                IsDeadEnd: false
            ),
        };

        return new GateInput(
            longLastRoom,
            shortLastRoom,
            gatedConnector,
            [gatedConnector],
            placements
        );
    }
}
