using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Common.Events;
using TRPG.Application.Worlds.Commands;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Worlds.Commands;

public sealed class SetFrontDoorsLockedCommandTests(DatabaseFixture db)
    : IAsyncLifetime,
        IClassFixture<DatabaseFixture>
{
    private readonly Guid _worldId = Guid.NewGuid();
    private readonly RecordingDomainEventConsumer<DoorLocksChangedEvent> _doorLocksChanged = new();

    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private SetFrontDoorsLockedCommandHandler _handler = null!;

    public ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .AddSingleton<IDomainEventConsumer<DoorLocksChangedEvent>>(_doorLocksChanged)
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<SetFrontDoorsLockedCommandHandler>();
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_PublishesTheDoorsLocation_WhenTheLockChanges()
    {
        // Arrange
        var building = await SeedBuilding();
        var outside = await SeedFrontDoor(building.Id, isLocked: false);

        // Act
        await _handler.Handle(
            new SetFrontDoorsLockedCommand { BuildingIds = [building.Id], IsLocked = true },
            TestContext.Current.CancellationToken
        );

        // Assert
        var published = Assert.Single(_doorLocksChanged.Events);
        Assert.Equal(_worldId, published.WorldId);
        Assert.Equal([outside.Id], published.LocationIds);
    }

    [Fact]
    public async Task Handle_PublishesNothing_WhenTheLockAlreadyHasThatState()
    {
        // Arrange
        var building = await SeedBuilding();
        await SeedFrontDoor(building.Id, isLocked: true);

        // Act
        await _handler.Handle(
            new SetFrontDoorsLockedCommand { BuildingIds = [building.Id], IsLocked = true },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Empty(_doorLocksChanged.Events);
    }

    [Fact]
    public async Task Handle_LocksTheDoor_WhenTheLockChanges()
    {
        // Arrange
        var building = await SeedBuilding();
        await SeedFrontDoor(building.Id, isLocked: false);

        // Act
        await _handler.Handle(
            new SetFrontDoorsLockedCommand { BuildingIds = [building.Id], IsLocked = true },
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        Assert.True(
            await verifyContext.DoorConnectors.AnyAsync(
                door => door.WorldId == _worldId && door.IsLocked,
                TestContext.Current.CancellationToken
            )
        );
    }

    private async Task<Building> SeedBuilding()
    {
        var building = Builders.MakeBuilding(worldId: _worldId);
        _context.Buildings.Add(building);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return building;
    }

    private async Task<Location> SeedFrontDoor(Guid buildingId, bool isLocked)
    {
        var entranceRoom = Builders.MakeRoom(buildingId, worldId: _worldId);
        var outsideLocation = Builders.MakeLocation(_worldId);
        var entryConnector = Builders.MakeLocationConnector(
            outsideLocation.Id,
            destinationLocationId: entranceRoom.LocationId,
            worldId: _worldId,
            name: "Front Door",
            description: "The door leading in."
        );
        _context.Rooms.Add(entranceRoom);
        _context.Locations.Add(outsideLocation);
        _context.LocationConnectors.Add(entryConnector);
        _context.DoorConnectors.Add(
            Builders.MakeDoorConnector(entryConnector.Id, isLocked: isLocked, worldId: _worldId)
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return outsideLocation;
    }
}
