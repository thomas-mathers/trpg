using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Buildings.Commands;
using TRPG.Application.Buildings.Queries;
using TRPG.Application.CreatureJobs.Commands;
using TRPG.Application.Creatures.Commands;
using TRPG.Application.Scenes.Commands;
using TRPG.Data;
using TRPG.Data.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Scenes.Commands;

[Collection("Database")]
public sealed class SyncCommandTests(DatabaseFixture db) : IAsyncLifetime
{
    private static readonly Guid WorldId = Guid.NewGuid();

    private AddBuildingOwnerCommandHandler _addBuildingOwner = null!;
    private AddCreatureCommandHandler _addCreature = null!;
    private AddCreatureJobCommandHandler _addJob = null!;
    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private GetWorkstationsByLocationIdQueryHandler _getWorkstationsByLocationId = null!;
    private SyncCommandHandler _handler = null!;

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
        _handler = _serviceProvider.GetRequiredService<SyncCommandHandler>();
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    private async Task<Location> SeedLocation(Guid? roomId = null, Guid? districtId = null)
    {
        var location = Builders.MakeLocation(WorldId, roomId: roomId, districtId: districtId);
        _context.Locations.Add(location);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return location;
    }

    private async Task<Creature> SeedCreature(Guid? locationId = null)
    {
        var creature = Builders.MakeCreature(WorldId, locationId: locationId);
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

    [Fact]
    public async Task Handle_MovesCreatureIntoRoom_WhenSleepJobActive()
    {
        // Arrange
        var sleepLocation = await SeedLocation(roomId: Guid.NewGuid());
        var creature = await SeedCreature();
        await AddJob(
            Builders.MakeCreatureJob(
                creature.Id,
                action: CreatureJobAction.Sleep,
                startHour: 22,
                endHour: 6,
                locationId: sleepLocation.Id,
                priority: 100
            )
        );

        // Act — hour 23 falls inside the wraparound Sleep window
        await _handler.Handle(
            new SyncCommand
            {
                WorldId = WorldId,
                LocationId = sleepLocation.Id,
                CurrentDate = Builders.MakeDate(23),
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        var updated = await verifyContext.Creatures.FindAsync(
            [creature.Id],
            TestContext.Current.CancellationToken
        );
        Assert.Equal(sleepLocation.Id, updated!.LocationId);
    }

    [Fact]
    public async Task Handle_MovesCreatureOut_WhenHigherPriorityWorkJobActiveElsewhere()
    {
        // Arrange
        var sleepLocation = await SeedLocation(roomId: Guid.NewGuid());
        var workLocation = await SeedLocation(roomId: Guid.NewGuid());
        var creature = await SeedCreature(sleepLocation.Id);
        await AddJob(
            Builders.MakeCreatureJob(
                creature.Id,
                action: CreatureJobAction.Sleep,
                startHour: 22,
                endHour: 6,
                locationId: sleepLocation.Id,
                priority: 100
            )
        );
        await AddJob(
            Builders.MakeCreatureJob(
                creature.Id,
                action: CreatureJobAction.Work,
                startHour: 8,
                endHour: 20,
                locationId: workLocation.Id,
                priority: 50
            )
        );

        // Act — hour 10 is inside Work, and the creature is discovered via their stale Sleep-location assignment
        await _handler.Handle(
            new SyncCommand
            {
                WorldId = WorldId,
                LocationId = sleepLocation.Id,
                CurrentDate = Builders.MakeDate(10),
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        var updated = await verifyContext.Creatures.FindAsync(
            [creature.Id],
            TestContext.Current.CancellationToken
        );
        Assert.Equal(workLocation.Id, updated!.LocationId);
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
            new SyncCommand
            {
                WorldId = WorldId,
                LocationId = emptyLocation.Id,
                CurrentDate = Builders.MakeDate(12),
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
    public async Task Handle_MovesCreatureOutdoors_WhenIdleJobActive()
    {
        // Arrange
        var districtId = Guid.NewGuid();
        var sleepLocation = await SeedLocation(roomId: Guid.NewGuid(), districtId: districtId);
        var idleLocation = await SeedLocation(districtId: districtId);
        var creature = await SeedCreature(sleepLocation.Id);
        await AddJob(
            Builders.MakeCreatureJob(
                creature.Id,
                action: CreatureJobAction.Sleep,
                startHour: 22,
                endHour: 6,
                locationId: sleepLocation.Id,
                priority: 100
            )
        );
        await AddJob(
            Builders.MakeCreatureJob(
                creature.Id,
                action: CreatureJobAction.Idle,
                startHour: 6,
                endHour: 22,
                locationId: idleLocation.Id,
                priority: 0
            )
        );

        // Act — hour 12 is inside Idle; the district catch-up discovers the creature via its
        // current (indoor) location, which shares the same district as the outdoor idle spot
        await _handler.Handle(
            new SyncCommand
            {
                WorldId = WorldId,
                LocationId = idleLocation.Id,
                CurrentDate = Builders.MakeDate(12),
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        var updated = await verifyContext.Creatures.FindAsync(
            [creature.Id],
            TestContext.Current.CancellationToken
        );
        Assert.Equal(idleLocation.Id, updated!.LocationId);
    }

    [Fact]
    public async Task Handle_AssignsBothWorkersToDifferentWorkstations_WhenTwoArePresent()
    {
        // Arrange
        var shopRoomId = Guid.NewGuid();
        var shopLocation = await SeedLocation(roomId: shopRoomId);
        var counter = new Workstation
        {
            LocationId = shopLocation.Id,
            WorldId = WorldId,
            Name = "Counter",
            Description = "A counter.",
            WorkstationType = WorkstationType.Trade,
        };
        var oven = new Workstation
        {
            LocationId = shopLocation.Id,
            WorldId = WorldId,
            Name = "Oven",
            Description = "An oven.",
            WorkstationType = WorkstationType.Cooking,
        };
        _context.Props.AddRange(counter, oven);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var owner = await SeedCreature();
        var employee = await SeedCreature();
        await AddJob(
            Builders.MakeCreatureJob(
                owner.Id,
                action: CreatureJobAction.Work,
                startHour: 8,
                endHour: 20,
                locationId: shopLocation.Id,
                priority: 50
            )
        );
        await AddJob(
            Builders.MakeCreatureJob(
                employee.Id,
                action: CreatureJobAction.Work,
                startHour: 8,
                endHour: 20,
                locationId: shopLocation.Id,
                priority: 50
            )
        );

        // Act
        await _handler.Handle(
            new SyncCommand
            {
                WorldId = WorldId,
                LocationId = shopLocation.Id,
                CurrentDate = Builders.MakeDate(12),
            },
            TestContext.Current.CancellationToken
        );

        // Assert — both workstations get staffed, by two different people
        var workstations = await _getWorkstationsByLocationId.Handle(
            new GetWorkstationsByLocationIdQuery { LocationId = shopLocation.Id },
            TestContext.Current.CancellationToken
        );
        var updatedCounter = workstations.First(w => w.Id == counter.Id);
        var updatedOven = workstations.First(w => w.Id == oven.Id);
        Assert.NotNull(updatedCounter.OccupantId);
        Assert.NotNull(updatedOven.OccupantId);
        Assert.NotEqual(updatedCounter.OccupantId, updatedOven.OccupantId);
        Assert.Contains(updatedCounter.OccupantId!.Value, new[] { owner.Id, employee.Id });
        Assert.Contains(updatedOven.OccupantId!.Value, new[] { owner.Id, employee.Id });
    }

    [Fact]
    public async Task Handle_GivesCounterPriority_AndClearsUnstaffedStation_WhenOnlyOneWorkerPresent()
    {
        // Arrange
        var shopRoomId = Guid.NewGuid();
        var shopLocation = await SeedLocation(roomId: shopRoomId);
        var counter = new Workstation
        {
            LocationId = shopLocation.Id,
            WorldId = WorldId,
            Name = "Counter",
            Description = "A counter.",
            WorkstationType = WorkstationType.Trade,
        };
        var oven = new Workstation
        {
            LocationId = shopLocation.Id,
            WorldId = WorldId,
            Name = "Oven",
            Description = "An oven.",
            WorkstationType = WorkstationType.Cooking,
            OccupantId = Guid.NewGuid(), // stale, from a prior day
        };
        _context.Props.AddRange(counter, oven);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var owner = await SeedCreature();
        await AddJob(
            Builders.MakeCreatureJob(
                owner.Id,
                action: CreatureJobAction.Work,
                startHour: 8,
                endHour: 20,
                locationId: shopLocation.Id,
                priority: 50
            )
        );

        // Act
        await _handler.Handle(
            new SyncCommand
            {
                WorldId = WorldId,
                LocationId = shopLocation.Id,
                CurrentDate = Builders.MakeDate(12),
            },
            TestContext.Current.CancellationToken
        );

        // Assert — the lone worker always gets the counter, and the unstaffed production station is cleared
        var workstations = await _getWorkstationsByLocationId.Handle(
            new GetWorkstationsByLocationIdQuery { LocationId = shopLocation.Id },
            TestContext.Current.CancellationToken
        );
        var updatedCounter = workstations.First(w => w.Id == counter.Id);
        var updatedOven = workstations.First(w => w.Id == oven.Id);
        Assert.Equal(owner.Id, updatedCounter.OccupantId);
        Assert.Null(updatedOven.OccupantId);
    }

    [Fact]
    public async Task Handle_LocksFrontDoor_WhenOwnerHasActiveSleepJob()
    {
        // Arrange
        var owner = await SeedCreature();
        var building = await SeedBuilding(owner.Id);
        var entranceRoom = await SeedEntranceRoom(building.Id);
        var frontDoor = await SeedFrontDoor(entranceRoom);
        var doorLocation = await SeedLocation(roomId: entranceRoom.Id);
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
            new SyncCommand
            {
                WorldId = WorldId,
                LocationId = doorLocation.Id,
                CurrentDate = Builders.MakeDate(23),
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        var updatedDoor = await _context
            .Props.AsNoTracking()
            .OfType<LocationConnector>()
            .FirstAsync(c => c.Id == frontDoor.Id, TestContext.Current.CancellationToken);
        Assert.True(updatedDoor.IsLocked);
    }

    private async Task<Building> SeedBuilding(Guid ownerId)
    {
        var building = Builders.MakeBuilding(Guid.NewGuid(), worldId: WorldId);
        _context.Buildings.Add(building);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        await _addBuildingOwner.Handle(
            new AddBuildingOwnerCommand { BuildingId = building.Id, OwnerId = ownerId },
            TestContext.Current.CancellationToken
        );
        return building;
    }

    private async Task<Room> SeedEntranceRoom(Guid buildingId)
    {
        var entranceRoom = Builders.MakeRoom(buildingId, worldId: WorldId);
        _context.Rooms.Add(entranceRoom);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return entranceRoom;
    }

    private async Task<LocationConnector> SeedFrontDoor(Room entranceRoom)
    {
        var outsideLocation = Builders.MakeLocation(WorldId);
        var frontDoor = Builders.MakeLocationConnector(
            entranceRoom.LocationId,
            destinationLocationId: outsideLocation.Id,
            worldId: WorldId,
            name: "Front Door",
            description: "The door leading outside."
        );
        _context.Locations.Add(outsideLocation);
        _context.Props.Add(frontDoor);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return frontDoor;
    }
}
