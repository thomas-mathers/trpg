using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Creatures;
using TRPG.Application.Creatures.Commands;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Creatures.Commands;

public sealed class FlushPlayerPoseCommandHandlerTests(DatabaseFixture db)
    : IAsyncLifetime,
        IClassFixture<DatabaseFixture>
{
    private static readonly Guid WorldId = Guid.NewGuid();

    private readonly Guid _stateId = Guid.NewGuid();
    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private FlushPlayerPoseCommandHandler _handler = null!;
    private PlayerPoseStore _poseStore = null!;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<FlushPlayerPoseCommandHandler>();
        _poseStore = _serviceProvider.GetRequiredService<PlayerPoseStore>();

        _context.States.Add(Builders.MakeState(Guid.NewGuid(), worldId: WorldId, id: _stateId));
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_WritesTheLivePoseToTheCreatureRow()
    {
        // Arrange
        var location = Builders.MakeLocation(WorldId, _stateId, width: 50, depth: 50);
        var player = Builders.MakeCreature(WorldId, locationId: location.Id);
        _context.Locations.Add(location);
        _context.Creatures.Add(player);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        _poseStore.Set(player.Id, MakePose(location.Id, x: 12, y: 7, angle: 2));

        // Act
        await _handler.Handle(
            new FlushPlayerPoseCommand { PlayerId = player.Id },
            TestContext.Current.CancellationToken
        );

        // Assert
        var stored = await ReadCreature(player.Id);
        Assert.Equal((12d, 7d, 2d), (stored.X, stored.Y, stored.Angle));
    }

    [Fact]
    public async Task Handle_WritesNothingTwice_WhenThePoseHasNotChangedSinceTheLastFlush()
    {
        // Arrange
        var location = Builders.MakeLocation(WorldId, _stateId, width: 50, depth: 50);
        var player = Builders.MakeCreature(WorldId, locationId: location.Id);
        _context.Locations.Add(location);
        _context.Creatures.Add(player);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        _poseStore.Set(player.Id, MakePose(location.Id, x: 12, y: 7, angle: 2));
        await _handler.Handle(
            new FlushPlayerPoseCommand { PlayerId = player.Id },
            TestContext.Current.CancellationToken
        );
        await _context
            .Creatures.Where(creature => creature.Id == player.Id)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(creature => creature.X, 40d),
                TestContext.Current.CancellationToken
            );

        // Act
        await _handler.Handle(
            new FlushPlayerPoseCommand { PlayerId = player.Id },
            TestContext.Current.CancellationToken
        );

        // Assert
        var stored = await ReadCreature(player.Id);
        Assert.Equal(40d, stored.X);
    }

    [Fact]
    public async Task Handle_LeavesTheRowAlone_WhenThePlayerHasMovedToAnotherLocation()
    {
        // Arrange
        var location = Builders.MakeLocation(WorldId, _stateId, width: 50, depth: 50);
        var newLocation = Builders.MakeLocation(WorldId, _stateId, width: 50, depth: 50);
        var player = Builders.MakeCreature(WorldId, locationId: newLocation.Id, x: 3, y: 4);
        _context.Locations.AddRange(location, newLocation);
        _context.Creatures.Add(player);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        _poseStore.Set(player.Id, MakePose(location.Id, x: 12, y: 7, angle: 2));

        // Act
        await _handler.Handle(
            new FlushPlayerPoseCommand { PlayerId = player.Id },
            TestContext.Current.CancellationToken
        );

        // Assert
        var stored = await ReadCreature(player.Id);
        Assert.Equal((3d, 4d), (stored.X, stored.Y));
    }

    [Fact]
    public async Task Handle_LeavesTheSeatPoseAlone_WhenThePlayerIsSitting()
    {
        // Arrange
        var location = Builders.MakeLocation(WorldId, _stateId, width: 50, depth: 50);
        var player = Builders.MakeCreature(
            WorldId,
            locationId: location.Id,
            posture: CreaturePosture.Sitting,
            x: 3,
            y: 4
        );
        _context.Locations.Add(location);
        _context.Creatures.Add(player);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        _poseStore.Set(player.Id, MakePose(location.Id, x: 12, y: 7, angle: 2));

        // Act
        await _handler.Handle(
            new FlushPlayerPoseCommand { PlayerId = player.Id },
            TestContext.Current.CancellationToken
        );

        // Assert
        var stored = await ReadCreature(player.Id);
        Assert.Equal((3d, 4d), (stored.X, stored.Y));
    }

    private async Task<Creature> ReadCreature(Guid id)
    {
        await using var verifyContext = db.CreateContext();

        return await verifyContext
            .Creatures.AsNoTracking()
            .SingleAsync(creature => creature.Id == id, TestContext.Current.CancellationToken);
    }

    private static PlayerPose MakePose(Guid locationId, double x, double y, double angle) =>
        new(locationId, x, y, angle, DateTimeOffset.UnixEpoch, IsDirty: true);
}
