using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.LocationSimulation;
using TRPG.Application.LocationSimulation.Commands;
using TRPG.Data;
using TRPG.Domain;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.LocationSimulation.Commands;

[Collection("Database")]
public sealed class ResetAlertedCreaturesCommandHandlerTests(DatabaseFixture db) : IAsyncLifetime
{
    private static readonly Guid WorldId = Guid.NewGuid();

    private readonly Guid _stateId = Guid.NewGuid();
    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private ResetAlertedCreaturesCommandHandler _handler = null!;
    private Location _location = null!;
    private GameSession _session = null!;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();

        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<ResetAlertedCreaturesCommandHandler>();

        var state = Builders.MakeState(Guid.NewGuid(), worldId: WorldId, id: _stateId);
        _location = Builders.MakeLocation(WorldId, _stateId);
        _session = Builders.MakeGameSession(WorldId, Guid.NewGuid());
        _context.States.Add(state);
        _context.Locations.Add(_location);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_EvictsCatchUpCacheAndClearsAlert_ForAlertedCreaturesAtTheLocation()
    {
        // Arrange — the session's fresh Playtime maps to in-game hour 8
        var alertedMonster = Builders.MakeCreature(
            WorldId,
            locationId: _location.Id,
            state: CreatureState.Alerted
        );
        _context.Creatures.Add(alertedMonster);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var catchUpCache = _serviceProvider.GetRequiredService<LocationCatchUpCache>();
        var currentDate = GameClock.GetCurrentInGameDate(_session.Playtime);
        catchUpCache.TryClaim(WorldId, _location.Id, currentDate);

        // Act
        await _handler.Handle(
            new ResetAlertedCreaturesCommand
            {
                WorldId = WorldId,
                LocationId = _location.Id,
                Playtime = _session.Playtime,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.True(catchUpCache.TryClaim(WorldId, _location.Id, currentDate));

        await using var verifyContext = db.CreateContext();
        var updatedMonster = await verifyContext.Creatures.FindAsync(
            [alertedMonster.Id],
            TestContext.Current.CancellationToken
        );
        Assert.Equal(CreatureState.Idle, updatedMonster!.State);
    }

    [Fact]
    public async Task Handle_DoesNothing_WhenNoCreaturesAreAlerted()
    {
        // Arrange
        var idleMonster = Builders.MakeCreature(
            WorldId,
            locationId: _location.Id,
            state: CreatureState.Idle
        );
        _context.Creatures.Add(idleMonster);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var catchUpCache = _serviceProvider.GetRequiredService<LocationCatchUpCache>();
        var currentDate = GameClock.GetCurrentInGameDate(_session.Playtime);
        catchUpCache.TryClaim(WorldId, _location.Id, currentDate);

        // Act
        await _handler.Handle(
            new ResetAlertedCreaturesCommand
            {
                WorldId = WorldId,
                LocationId = _location.Id,
                Playtime = _session.Playtime,
            },
            TestContext.Current.CancellationToken
        );

        // Assert - nothing to reset, so the cache entry is left untouched
        Assert.False(catchUpCache.TryClaim(WorldId, _location.Id, currentDate));
    }
}
