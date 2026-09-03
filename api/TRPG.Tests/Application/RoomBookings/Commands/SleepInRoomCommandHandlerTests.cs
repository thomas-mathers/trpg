using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.RoomBookings.Commands;
using TRPG.Data;
using TRPG.Domain;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.RoomBookings.Commands;

[Collection("Database")]
public sealed class SleepInRoomCommandHandlerTests(DatabaseFixture db) : IAsyncLifetime
{
    private static readonly Guid WorldId = Guid.NewGuid();

    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private SleepInRoomCommandHandler _handler = null!;
    private readonly Guid _locationId = Guid.NewGuid();
    private readonly Creature _player = Builders.MakeCreature(WorldId, currentHp: 0);
    private GameSession _session = null!;
    private Bed _bed = null!;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<SleepInRoomCommandHandler>();

        _session = Builders.MakeGameSession(WorldId, _player.Id, playtime: TimeSpan.FromHours(8));
        _bed = Builders.MakeBed(WorldId, locationId: _locationId, assignedCreatureId: _player.Id);

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
    public async Task Handle_AdvancesPlaytimeRegeneratesAndSetsRestedUntil_WhenDeltaIsAtLeastOneHour()
    {
        // Arrange
        var delta = GameClock.RealTimePerInGameHour * 8;

        // Act
        var outcome = await _handler.Handle(
            new SleepInRoomCommand
            {
                PlayerId = _player.Id,
                SessionId = _session.Id,
                LocationId = _locationId,
                Delta = delta,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(SleepOutcome.Slept, outcome);

        await using var verifyContext = db.CreateContext();
        var session = await verifyContext.GameSessions.SingleAsync(
            s => s.Id == _session.Id,
            TestContext.Current.CancellationToken
        );
        var expectedPlaytime = _session.Playtime + delta;
        Assert.Equal(expectedPlaytime, session.Playtime);

        var updatedPlayer = await verifyContext.Creatures.SingleAsync(
            c => c.Id == _player.Id,
            TestContext.Current.CancellationToken
        );
        Assert.True(updatedPlayer.CurrentHp > 0);
        Assert.Equal(
            expectedPlaytime + GameClock.RealTimePerInGameHour * 24,
            updatedPlayer.RestedUntilPlaytime
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
                SessionId = _session.Id,
                LocationId = _locationId,
                Delta = GameClock.RealTimePerInGameHour * 0.5,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        var updatedPlayer = await verifyContext.Creatures.SingleAsync(
            c => c.Id == _player.Id,
            TestContext.Current.CancellationToken
        );
        Assert.Null(updatedPlayer.RestedUntilPlaytime);
    }

    [Fact]
    public async Task Handle_ReturnsNotYourRoom_WhenNoBedExistsAtTheLocation()
    {
        // Act
        var outcome = await _handler.Handle(
            new SleepInRoomCommand
            {
                PlayerId = _player.Id,
                SessionId = _session.Id,
                LocationId = Guid.NewGuid(),
                Delta = GameClock.RealTimePerInGameHour * 8,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(SleepOutcome.NotYourRoom, outcome);
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
                SessionId = _session.Id,
                LocationId = _locationId,
                Delta = GameClock.RealTimePerInGameHour * 8,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(SleepOutcome.NotYourRoom, outcome);
    }
}
