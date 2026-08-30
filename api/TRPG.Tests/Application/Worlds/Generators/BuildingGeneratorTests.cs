using TRPG.Application.Worlds.Generators;
using TRPG.Domain.Models;

namespace TRPG.Tests.Application.Worlds.Generators;

public class BuildingGeneratorTests
{
    private readonly Guid _worldId = Guid.NewGuid();

    [Fact]
    public void Generate_AssignsOwnerToEveryProp_WhenBuildingHasAnOwner()
    {
        // Arrange
        var ownerCreatureId = Guid.NewGuid();

        // Act
        var result = GenerateBuilding(ownerCreatureId);

        // Assert
        Assert.NotEmpty(result.Props);
        Assert.All(result.Props, prop => Assert.Equal(ownerCreatureId, prop.OwnerCreatureId));
    }

    [Fact]
    public void Generate_LeavesPropsUnowned_WhenBuildingHasNoOwner()
    {
        // Act
        var result = GenerateBuilding(ownerCreatureId: null);

        // Assert
        Assert.NotEmpty(result.Props);
        Assert.All(result.Props, prop => Assert.Null(prop.OwnerCreatureId));
    }

    [Fact]
    public void Generate_MakesEveryGuestRoomReachableFromTheLobby_ForInn()
    {
        // Act
        var result = GenerateInn();

        // Assert
        var lobby = result.Rooms.Single(r => r.FloorNumber == 0);
        var guestRooms = result
            .Rooms.Where(r => r.Name.EndsWith("Guest Room", StringComparison.Ordinal))
            .ToArray();
        var reachableLocationIds = ReachableLocationIds(
            result.LocationConnectors,
            lobby.LocationId
        );

        Assert.Equal(4, guestRooms.Length);
        Assert.All(guestRooms, room => Assert.Contains(room.LocationId, reachableLocationIds));
    }

    [Fact]
    public void Generate_CreatesAKeyedDoorOwnedByTheCounter_ForEveryGuestRoom()
    {
        // Act
        var result = GenerateInn();

        // Assert
        var guestRooms = result
            .Rooms.Where(r => r.Name.EndsWith("Guest Room", StringComparison.Ordinal))
            .ToArray();
        var counter = result
            .Props.OfType<Workstation>()
            .Single(w => w.WorkstationType == WorkstationType.Trade);

        foreach (var guestRoom in guestRooms)
        {
            var entryConnector = result.LocationConnectors.Single(c =>
                c.DestinationLocationId == guestRoom.LocationId
            );
            var door = Assert.Single(result.InteriorDoors, d => d.ConnectorId == entryConnector.Id);
            Assert.True(door.IsLocked);

            var doorKey = Assert.Single(
                result.DoorConnectorKeys,
                k => k.DoorConnectorId == door.Id
            );
            var keyItem = Assert.Single(result.KeyItems, i => i.Id == doorKey.ItemId);
            Assert.Equal(OwnerType.Workstation, keyItem.Ownership.OwnerType);
            Assert.Equal(counter.Id, keyItem.Ownership.OwnerId);
        }
    }

    private static HashSet<Guid> ReachableLocationIds(
        IReadOnlyList<LocationConnector> connectors,
        Guid startLocationId
    )
    {
        var reachable = new HashSet<Guid> { startLocationId };
        var frontier = new Queue<Guid>();
        frontier.Enqueue(startLocationId);

        while (frontier.Count > 0)
        {
            var current = frontier.Dequeue();
            foreach (var connector in connectors.Where(c => c.OriginLocationId == current))
            {
                if (reachable.Add(connector.DestinationLocationId))
                {
                    frontier.Enqueue(connector.DestinationLocationId);
                }
            }
        }

        return reachable;
    }

    private BuildingGeneratorResult GenerateBuilding(Guid? ownerCreatureId)
    {
        var exteriorLocation = new Location
        {
            WorldId = _worldId,
            StateId = Guid.NewGuid(),
            Kind = LocationKind.District,
        };
        var spec = BuildingSpecCatalog.GetSpecs(
            BuildingType.Blacksmith,
            ownerCreatureId,
            [],
            bedroomGroups: null
        );

        return new BuildingGenerator().Generate(
            new BuildingGeneratorInput(exteriorLocation, spec)
            {
                Name = "Test blacksmith",
                OwnerCreatureId = ownerCreatureId,
            }
        );
    }

    private BuildingGeneratorResult GenerateInn()
    {
        var ownerCreatureId = Guid.NewGuid();
        var exteriorLocation = new Location
        {
            WorldId = _worldId,
            StateId = Guid.NewGuid(),
            Kind = LocationKind.District,
        };
        var spec = BuildingSpecCatalog.GetSpecs(
            BuildingType.Inn,
            ownerCreatureId,
            [],
            bedroomGroups: null
        );

        return new BuildingGenerator().Generate(
            new BuildingGeneratorInput(exteriorLocation, spec)
            {
                Name = "Test inn",
                OwnerCreatureId = ownerCreatureId,
            }
        );
    }
}
