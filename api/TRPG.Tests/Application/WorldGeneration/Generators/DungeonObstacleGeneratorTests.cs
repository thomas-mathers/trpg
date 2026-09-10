using TRPG.Application.WorldGeneration.Generators;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.WorldGeneration.Generators;

public class DungeonObstacleGeneratorTests
{
    private readonly Guid _worldId = Guid.NewGuid();
    private readonly Guid _stateId = Guid.NewGuid();
    private readonly Guid _buildingId = Guid.NewGuid();
    private readonly Guid _entranceLocationId = Guid.NewGuid();
    private readonly IReadOnlyDictionary<CreatureType, Faction> _factionsByCreatureType =
        EncounterFactionGenerator.Generate(Guid.NewGuid());
    private readonly DungeonObstacleGenerator _generator = new(
        new DungeonPopulator(Builders.MakeCreatureGenerator())
    );

    private readonly Room _shortEntryRoom = new()
    {
        BuildingId = Guid.NewGuid(),
        LocationId = Guid.NewGuid(),
        Name = "Short Entry",
        WorldId = Guid.NewGuid(),
    };
    private readonly Room _longFirstRoom = new()
    {
        BuildingId = Guid.NewGuid(),
        LocationId = Guid.NewGuid(),
        Name = "Long First",
        WorldId = Guid.NewGuid(),
    };

    private DungeonObstacleInput MakeInput()
    {
        var placements = new[]
        {
            new DungeonRoomPlacement(
                _shortEntryRoom,
                RoomRole.Passage,
                DepthFromEntrance: 1,
                FloorNumber: 0,
                RouteKind: DungeonRouteKind.Short,
                IsDeadEnd: false
            ),
            new DungeonRoomPlacement(
                _longFirstRoom,
                RoomRole.Passage,
                DepthFromEntrance: 1,
                FloorNumber: 0,
                RouteKind: DungeonRouteKind.Long,
                IsDeadEnd: false
            ),
        };
        var entryConnector = new LocationConnector
        {
            OriginLocationId = _entranceLocationId,
            DestinationLocationId = _shortEntryRoom.LocationId,
            DestinationLabel = _shortEntryRoom.Name,
            WorldId = _worldId,
        };

        return new DungeonObstacleInput(
            placements,
            [entryConnector],
            _entranceLocationId,
            _buildingId,
            BuildingType.Cave,
            _worldId,
            _stateId,
            _factionsByCreatureType,
            Random.Shared
        );
    }

    [Fact]
    public void Generate_LocksTheShortRoutesEntryConnector_ForKeyLock()
    {
        // Arrange
        var input = MakeInput();
        var entryConnector = input.LocationConnectors[0];

        // Act
        var result = _generator.Generate(input, DungeonObstacleKind.KeyLock);

        // Assert
        var door = Assert.Single(result.DoorConnectors);
        Assert.Equal(entryConnector.Id, door.ConnectorId);
        Assert.True(door.IsLocked);
    }

    [Fact]
    public void Generate_PutsTheKeyOnAGuard_GuardedBehindASpurOffTheLongRoute()
    {
        // Arrange
        var input = MakeInput();

        // Act
        var result = _generator.Generate(input, DungeonObstacleKind.KeyLock);

        // Assert — a two-room spur (Side Passage, then the guarded alcove), not a single adjacent
        // room, and rooted off the long route rather than the short route it gates.
        Assert.Equal(2, result.Rooms.Count);
        var guard = Assert.Single(result.Monsters);
        var key = Assert.Single(result.Items);
        Assert.Equal(guard.Creature.Id, key.Ownership.OwnerId);
        Assert.Equal(OwnerType.Creature, key.Ownership.OwnerType);

        var doorConnectorKey = Assert.Single(result.DoorConnectorKeys);
        Assert.Equal(key.Id, doorConnectorKey.ItemId);
        Assert.Equal(result.DoorConnectors[0].Id, doorConnectorKey.DoorConnectorId);
    }

    [Fact]
    public void Generate_ConnectsTheKeySpur_ToTheLongRoutesFirstRoom()
    {
        // Arrange
        var input = MakeInput();

        // Act
        var result = _generator.Generate(input, DungeonObstacleKind.KeyLock);

        // Assert
        Assert.Contains(
            result.LocationConnectors,
            connector => connector.OriginLocationId == _longFirstRoom.LocationId
        );
        Assert.Contains(
            result.LocationConnectors,
            connector => connector.DestinationLocationId == _longFirstRoom.LocationId
        );
    }

    [Fact]
    public void Generate_ForcesAnOccupant_AtTheShortRoutesLastRoom_ForMiniboss()
    {
        // Arrange
        var input = MakeInput();

        // Act
        var result = _generator.Generate(input, DungeonObstacleKind.Miniboss);

        // Assert
        var miniboss = Assert.Single(result.Monsters);
        Assert.Equal(_shortEntryRoom.LocationId, miniboss.Creature.LocationId);
        Assert.Empty(result.DoorConnectors);
        Assert.Empty(result.Rooms);
    }

    [Fact]
    public void Generate_SpawnsAMinibossAtAHigherLevel_ThanAnOrdinaryOccupant()
    {
        // Arrange
        var input = MakeInput();
        var ordinaryLevels = new List<int>();
        var minibossLevels = new List<int>();

        for (var i = 0; i < 20; i++)
        {
            // Act
            ordinaryLevels.Add(
                new DungeonPopulator(Builders.MakeCreatureGenerator())
                    .GenerateForced(
                        _worldId,
                        Guid.NewGuid(),
                        BuildingType.Cave,
                        playerLevel: 1,
                        _factionsByCreatureType
                    )
                    .Monsters.Single()
                    .Creature.Level
            );
            minibossLevels.Add(
                _generator
                    .Generate(input, DungeonObstacleKind.Miniboss)
                    .Monsters.Single()
                    .Creature.Level
            );
        }

        // Assert
        Assert.True(minibossLevels.Average() > ordinaryLevels.Average());
    }

    [Fact]
    public void Generate_ReturnsEmpty_WhenTheDungeonHasNoShortRoute()
    {
        // Arrange
        var input = MakeInput() with
        {
            Placements =
            [
                new DungeonRoomPlacement(
                    _longFirstRoom,
                    RoomRole.Passage,
                    1,
                    0,
                    DungeonRouteKind.Long,
                    false
                ),
            ],
        };

        // Act
        var result = _generator.Generate(input, DungeonObstacleKind.KeyLock);

        // Assert
        Assert.Empty(result.DoorConnectors);
        Assert.Empty(result.Monsters);
    }

    [Fact]
    public void Generate_PlacesOneTrapPerRoom_CyclingThroughEveryKind_ForTrapGauntlet()
    {
        // Arrange — four short-route rooms so all four kinds get exercised in order.
        var input = MakeGauntletInput(roomCount: 4);

        // Act
        var result = _generator.Generate(input, DungeonObstacleKind.TrapGauntlet);

        // Assert
        Assert.Equal(4, result.Triggers.Count);
        Assert.Equal(
            [TrapKind.Mechanical, TrapKind.Collapse, TrapKind.Slope, TrapKind.Water],
            result
                .Triggers.OrderBy(trigger =>
                    input
                        .Placements.Single(p => p.Room.LocationId == trigger.LocationId)
                        .DepthFromEntrance
                )
                .Select(trigger => trigger.TrapKind)
        );
    }

    [Fact]
    public void Generate_GauntletMechanicalTrapGetsARubbleLanding_SoItCanNeverSoftLock()
    {
        // Arrange — a single-room gauntlet, so Mechanical has no companion Collapse/Slope trap.
        var input = MakeGauntletInput(roomCount: 1);

        // Act
        var result = _generator.Generate(input, DungeonObstacleKind.TrapGauntlet);

        // Assert
        var trap = Assert.Single(result.Triggers);
        Assert.Equal(TrapKind.Mechanical, trap.TrapKind);
        var trapCellar = result.Rooms.Single(room => room.Name == "Trap Cellar");
        var rubbleLanding = result.Rooms.Single(room => room.Name == "Rubble Landing");
        Assert.Equal(trapCellar.LocationId, trap.TargetId);
        Assert.Contains(
            result.LocationConnectors,
            connector =>
                connector.OriginLocationId == trapCellar.LocationId
                && connector.DestinationLocationId == rubbleLanding.LocationId
        );
    }

    private DungeonObstacleInput MakeGauntletInput(int roomCount)
    {
        var gauntletRooms = Enumerable
            .Range(1, roomCount)
            .Select(depth => new DungeonRoomPlacement(
                new Room
                {
                    BuildingId = _buildingId,
                    LocationId = Guid.NewGuid(),
                    Name = $"Gauntlet {depth}",
                    WorldId = _worldId,
                },
                RoomRole.Passage,
                DepthFromEntrance: depth,
                FloorNumber: 0,
                RouteKind: DungeonRouteKind.Short,
                IsDeadEnd: false
            ))
            .ToArray();
        var placements = gauntletRooms
            .Concat([
                new DungeonRoomPlacement(
                    _longFirstRoom,
                    RoomRole.Passage,
                    DepthFromEntrance: 1,
                    FloorNumber: 0,
                    RouteKind: DungeonRouteKind.Long,
                    IsDeadEnd: false
                ),
            ])
            .ToArray();

        return MakeInput() with
        {
            Placements = placements,
        };
    }
}
