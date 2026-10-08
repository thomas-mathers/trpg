using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.CreatureJobs.Commands;
using TRPG.Application.Creatures.Commands;
using TRPG.Application.Props.Queries;
using TRPG.Application.Worlds.Commands;
using TRPG.Application.WorldSimulation;
using TRPG.Application.WorldSimulation.Commands;
using TRPG.Data;
using TRPG.Domain;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.WorldSimulation.Commands;

public sealed class CatchUpLocationCommandTests(DatabaseFixture db)
    : IAsyncLifetime,
        IClassFixture<DatabaseFixture>
{
    private static readonly Guid WorldId = Guid.NewGuid();
    private static readonly Guid PlayerId = Guid.NewGuid();

    private AddBuildingOwnerCommandHandler _addBuildingOwner = null!;
    private AddCreatureCommandHandler _addCreature = null!;
    private AddCreatureJobCommandHandler _addJob = null!;
    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private GetWorkstationsByLocationIdQueryHandler _getWorkstationsByLocationId = null!;
    private CatchUpLocationCommandHandler _handler = null!;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();

        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _addJob = _serviceProvider.GetRequiredService<AddCreatureJobCommandHandler>();
        _addCreature = _serviceProvider.GetRequiredService<AddCreatureCommandHandler>();
        _addBuildingOwner = _serviceProvider.GetRequiredService<AddBuildingOwnerCommandHandler>();
        _getWorkstationsByLocationId =
            _serviceProvider.GetRequiredService<GetWorkstationsByLocationIdQueryHandler>();
        _handler = _serviceProvider.GetRequiredService<CatchUpLocationCommandHandler>();
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    private async Task<Location> SeedLocation(
        Guid? roomId = null,
        Guid? districtId = null,
        Guid? id = null
    )
    {
        var location = Builders.MakeLocation(
            WorldId,
            roomId: roomId,
            districtId: districtId,
            id: id
        );
        _context.Locations.Add(location);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return location;
    }

    private async Task<Creature> SeedCreature(Guid? locationId = null)
    {
        var creature = Builders.MakeCreature(WorldId, locationId: locationId);
        creature.MovementSpeed = 5;
        await _addCreature.Handle(
            new AddCreatureCommand { Creature = creature },
            TestContext.Current.CancellationToken
        );
        return creature;
    }

    private Task AddJob(CreatureJob job) =>
        _addJob.Handle(
            new AddCreatureJobCommand { CreatureJob = job },
            TestContext.Current.CancellationToken
        );

    private async Task<LocationConnector> AddConnector(
        Guid originLocationId,
        Guid destinationLocationId
    )
    {
        var connector = new LocationConnector
        {
            WorldId = WorldId,
            OriginLocationId = originLocationId,
            DestinationLocationId = destinationLocationId,
            DestinationLabel = "Destination",
        };
        _context.LocationConnectors.Add(connector);
        _context.TravelNodes.AddRange(Builders.MakeConnectorNodes(connector));
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return connector;
    }

    private async Task AddWalk(
        Guid locationId,
        LocationConnector arrival,
        LocationConnector departure
    )
    {
        _context.PointConnectors.Add(
            Builders.MakePointConnector(
                locationId,
                arrival.DestinationNodeId,
                departure.OriginNodeId,
                WalkPace.MetersFor(5, 1),
                WorldId
            )
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Handle_DoesNothing_WhenNoJobsTargetLocation()
    {
        // Arrange
        var creature = await SeedCreature();
        var originalLocationId = creature.LocationId;
        var emptyLocation = await SeedLocation(roomId: Guid.NewGuid());

        // Act
        await _handler.Handle(
            new CatchUpLocationCommand
            {
                WorldId = WorldId,
                PlayerId = PlayerId,
                LocationId = emptyLocation.Id,
                PlayerLevel = 1,
                GameTime = GameClock.Epoch,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        var updated = await verifyContext.Creatures.FindAsync(
            [creature.Id],
            TestContext.Current.CancellationToken
        );
        Assert.Equal(originalLocationId, updated!.LocationId);
    }

    [Fact]
    public async Task Handle_LocksFrontDoor_WhenOwnerHasActiveSleepJob()
    {
        // Arrange
        var owner = await SeedCreature();
        var building = await SeedBuilding(owner.Id);
        var entranceLocationId = Guid.NewGuid();
        var entranceRoom = await SeedEntranceRoom(building.Id, entranceLocationId);
        var frontDoor = await SeedFrontDoor(entranceRoom);
        var doorLocation = await SeedLocation(roomId: entranceRoom.Id, id: entranceLocationId);
        await AddJob(
            Builders.MakeCreatureJob(
                owner.Id,
                action: CreatureJobAction.Sleep,
                startHour: 22,
                endHour: 6,
                priority: 100
            )
        );

        // Act — hour 23 falls inside the wraparound Sleep window
        await _handler.Handle(
            new CatchUpLocationCommand
            {
                WorldId = WorldId,
                PlayerId = PlayerId,
                LocationId = doorLocation.Id,
                PlayerLevel = 1,
                GameTime = GameClock.Epoch + TimeSpan.FromHours(1) * 15,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        var updatedDoor = await _context
            .DoorConnectors.AsNoTracking()
            .FirstAsync(c => c.Id == frontDoor.Id, TestContext.Current.CancellationToken);
        Assert.True(updatedDoor.IsLocked);
    }

    [Fact]
    public async Task Handle_CreatesWeatherState_ForTheLocationsState()
    {
        // Arrange
        var stateId = Guid.NewGuid();
        var location = Builders.MakeLocation(WorldId, stateId: stateId);
        _context.Locations.Add(location);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        await _handler.Handle(
            new CatchUpLocationCommand
            {
                WorldId = WorldId,
                PlayerId = PlayerId,
                LocationId = location.Id,
                PlayerLevel = 1,
                GameTime = GameClock.Epoch,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        Assert.True(
            await verifyContext.WeatherStates.AnyAsync(
                w => w.StateId == stateId,
                TestContext.Current.CancellationToken
            )
        );
    }

    private async Task<Building> SeedBuilding(Guid ownerId)
    {
        var building = Builders.MakeBuilding(worldId: WorldId);
        _context.Buildings.Add(building);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        await _addBuildingOwner.Handle(
            new AddBuildingOwnerCommand { BuildingId = building.Id, OwnerId = ownerId },
            TestContext.Current.CancellationToken
        );
        return building;
    }

    private async Task<Room> SeedEntranceRoom(Guid buildingId, Guid? locationId = null)
    {
        var entranceRoom = Builders.MakeRoom(buildingId, worldId: WorldId, locationId: locationId);
        _context.Rooms.Add(entranceRoom);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return entranceRoom;
    }

    private async Task<DoorConnector> SeedFrontDoor(Room entranceRoom)
    {
        var outsideLocation = Builders.MakeLocation(WorldId);
        var entryConnector = Builders.MakeLocationConnector(
            outsideLocation.Id,
            destinationLocationId: entranceRoom.LocationId,
            worldId: WorldId,
            name: "Front Door",
            description: "The door leading in."
        );
        var door = Builders.MakeDoorConnector(entryConnector.Id, worldId: WorldId);
        _context.Locations.Add(outsideLocation);
        _context.LocationConnectors.Add(entryConnector);
        _context.TravelNodes.AddRange(Builders.MakeConnectorNodes(entryConnector));
        _context.DoorConnectors.Add(door);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return door;
    }
}
