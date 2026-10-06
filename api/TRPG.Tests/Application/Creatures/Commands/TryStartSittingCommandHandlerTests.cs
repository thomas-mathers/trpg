using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Creatures;
using TRPG.Application.Creatures.Commands;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Creatures.Commands;

public sealed class TryStartSittingCommandHandlerTests(DatabaseFixture db)
    : IAsyncLifetime,
        IClassFixture<DatabaseFixture>
{
    private static readonly Guid WorldId = Guid.NewGuid();

    private readonly Guid _stateId = Guid.NewGuid();
    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private TryStartSittingCommandHandler _handler = null!;
    private PlayerPoseStore _poseStore = null!;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<TryStartSittingCommandHandler>();
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
    public async Task Handle_RemembersTheLivePoseAsTheStandingPose()
    {
        // Arrange
        var location = Builders.MakeLocation(WorldId, _stateId, width: 50, depth: 50);
        var player = Builders.MakeCreature(WorldId, locationId: location.Id, x: 3, y: 4);
        _context.Locations.Add(location);
        _context.Creatures.Add(player);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        _poseStore.Set(player.Id, MakePose(location.Id, x: 12, y: 7, angle: 2));

        // Act
        await _handler.Handle(
            MakeCommand(player.Id, location.Id),
            TestContext.Current.CancellationToken
        );

        // Assert
        var stored = await ReadCreature(player.Id);
        Assert.Equal((12d, 7d, 2d), (stored.StandingX, stored.StandingY, stored.StandingAngle));
    }

    [Fact]
    public async Task Handle_RemembersTheRowPoseAsTheStandingPose_WhenThereIsNoLivePose()
    {
        // Arrange
        var location = Builders.MakeLocation(WorldId, _stateId, width: 50, depth: 50);
        var player = Builders.MakeCreature(WorldId, locationId: location.Id, x: 3, y: 4);
        _context.Locations.Add(location);
        _context.Creatures.Add(player);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        await _handler.Handle(
            MakeCommand(player.Id, location.Id),
            TestContext.Current.CancellationToken
        );

        // Assert
        var stored = await ReadCreature(player.Id);
        Assert.Equal((3d, 4d), (stored.StandingX, stored.StandingY));
    }

    [Fact]
    public async Task Handle_IgnoresALivePoseFromAnotherLocation()
    {
        // Arrange
        var location = Builders.MakeLocation(WorldId, _stateId, width: 50, depth: 50);
        var otherLocation = Builders.MakeLocation(WorldId, _stateId, width: 50, depth: 50);
        var player = Builders.MakeCreature(WorldId, locationId: location.Id, x: 3, y: 4);
        _context.Locations.AddRange(location, otherLocation);
        _context.Creatures.Add(player);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        _poseStore.Set(player.Id, MakePose(otherLocation.Id, x: 12, y: 7, angle: 2));

        // Act
        await _handler.Handle(
            MakeCommand(player.Id, location.Id),
            TestContext.Current.CancellationToken
        );

        // Assert
        var stored = await ReadCreature(player.Id);
        Assert.Equal((3d, 4d), (stored.StandingX, stored.StandingY));
    }

    [Fact]
    public async Task Handle_MovesTheCreatureToTheSeat()
    {
        // Arrange
        var location = Builders.MakeLocation(WorldId, _stateId, width: 50, depth: 50);
        var player = Builders.MakeCreature(WorldId, locationId: location.Id, x: 3, y: 4);
        _context.Locations.Add(location);
        _context.Creatures.Add(player);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        _poseStore.Set(player.Id, MakePose(location.Id, x: 12, y: 7, angle: 2));

        // Act
        await _handler.Handle(
            MakeCommand(player.Id, location.Id),
            TestContext.Current.CancellationToken
        );

        // Assert
        var stored = await ReadCreature(player.Id);
        Assert.Equal((20d, 21d, 1d), (stored.X, stored.Y, stored.Angle));
    }

    private static TryStartSittingCommand MakeCommand(Guid creatureId, Guid locationId) =>
        new()
        {
            CreatureId = creatureId,
            LocationId = locationId,
            X = 20,
            Y = 21,
            Angle = 1,
        };

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
