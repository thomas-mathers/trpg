using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.RoomBookings.Commands;
using TRPG.Data;
using TRPG.Domain;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.RoomBookings.Commands;

public sealed class SleepInRoomCommandHandlerTests(DatabaseFixture db)
    : IAsyncLifetime,
        IClassFixture<DatabaseFixture>
{
    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private SleepInRoomCommandHandler _handler = null!;
    private Guid WorldId { get; set; }
    private readonly Guid _locationId = Guid.NewGuid();
    private Creature _player = null!;
    private GameSession _session = null!;
    private Bed _bed = null!;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<SleepInRoomCommandHandler>();

        WorldId = Guid.NewGuid();
        _player = Builders.MakeCreature(WorldId, currentHp: 0);
        _session = Builders.MakeGameSession(WorldId, _player.Id);
        var world = Builders.MakeWorld(WorldId, GameClock.Epoch + TimeSpan.FromHours(8));
        _bed = Builders.MakeBed(WorldId, locationId: _locationId, assignedCreatureId: _player.Id);

        _context.Worlds.Add(world);
        _context.Creatures.Add(_player);
        _context.GameSessions.Add(_session);
        _context.Props.Add(_bed);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_AdvancesGameTimeRegeneratesAndSetsRestedUntil_WhenDeltaIsAtLeastOneHour()
    {
        // Arrange
        var delta = TimeSpan.FromHours(1) * 8;

        // Act
        var outcome = await _handler.Handle(
            new SleepInRoomCommand
            {
                PlayerId = _player.Id,
                WorldId = WorldId,
                LocationId = _locationId,
                Delta = delta,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(SleepOutcome.Slept, outcome.Outcome);

        await using var verifyContext = db.CreateContext();
        var world = await verifyContext.Worlds.SingleAsync(
            world => world.Id == WorldId,
            TestContext.Current.CancellationToken
        );
        var expectedGameTime = GameClock.Epoch + TimeSpan.FromHours(8) + delta;
        Assert.Equal(expectedGameTime, world.GameTime);

        var updatedPlayer = await verifyContext.Creatures.SingleAsync(
            c => c.Id == _player.Id,
            TestContext.Current.CancellationToken
        );
        Assert.True(updatedPlayer.CurrentHp > 0);
        Assert.Equal(
            expectedGameTime + TimeSpan.FromHours(1) * 24,
            updatedPlayer.RestedUntilGameTime
        );
    }

    [Fact]
    public async Task Handle_DoesNotSetRestedUntil_WhenDeltaIsUnderOneHour()
    {
        // Act
        await _handler.Handle(
            new SleepInRoomCommand
            {
                PlayerId = _player.Id,
                WorldId = WorldId,
                LocationId = _locationId,
                Delta = TimeSpan.FromHours(1) * 0.5,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        var updatedPlayer = await verifyContext.Creatures.SingleAsync(
            c => c.Id == _player.Id,
            TestContext.Current.CancellationToken
        );
        Assert.Null(updatedPlayer.RestedUntilGameTime);
    }

    [Fact]
    public async Task Handle_ReturnsNotYourRoom_WhenNoBedExistsAtTheLocation()
    {
        // Act
        var outcome = await _handler.Handle(
            new SleepInRoomCommand
            {
                PlayerId = _player.Id,
                WorldId = WorldId,
                LocationId = Guid.NewGuid(),
                Delta = TimeSpan.FromHours(1) * 8,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(SleepOutcome.NotYourRoom, outcome.Outcome);
    }

    [Fact]
    public async Task Handle_ReturnsNotYourRoom_WhenTheBedIsAssignedToADifferentCreature()
    {
        // Arrange
        _bed.AssignedCreatureId = Guid.NewGuid();
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var outcome = await _handler.Handle(
            new SleepInRoomCommand
            {
                PlayerId = _player.Id,
                WorldId = WorldId,
                LocationId = _locationId,
                Delta = TimeSpan.FromHours(1) * 8,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(SleepOutcome.NotYourRoom, outcome.Outcome);
    }

    [Fact]
    public async Task Handle_Throws_WhenDeltaExceedsTwentyFourHours()
    {
        var action = () =>
            _handler.Handle(
                new SleepInRoomCommand
                {
                    PlayerId = _player.Id,
                    WorldId = WorldId,
                    LocationId = _locationId,
                    Delta = TimeSpan.FromHours(24) + TimeSpan.FromMinutes(1),
                },
                TestContext.Current.CancellationToken
            );

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(action);
    }
}
