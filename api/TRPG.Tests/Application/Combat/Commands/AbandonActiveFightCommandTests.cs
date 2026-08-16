using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Combat.Commands;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Combat.Commands;

[Collection("Database")]
public sealed class AbandonActiveFightCommandTests(DatabaseFixture db) : IAsyncLifetime
{
    private static readonly Guid WorldId = Guid.NewGuid();

    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private AbandonActiveFightCommandHandler _handler = null!;
    private readonly Creature _player = Builders.MakeCreature(WorldId, currentHp: 40);
    private readonly Creature _enemy = Builders.MakeCreature(
        WorldId,
        currentHp: 0,
        state: CreatureState.Dead
    );

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<AbandonActiveFightCommandHandler>();

        _context.Creatures.AddRange(_player, _enemy);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_DoesNothing_WhenNoActiveFight()
    {
        // Act
        await _handler.Handle(
            new AbandonActiveFightCommand
            {
                WorldId = WorldId,
                PlayerId = _player.Id,
                Playtime = TimeSpan.FromHours(3),
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        var fightCount = await _context.Fights.CountAsync(
            f => f.PlayerId == _player.Id,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(0, fightCount);
    }

    [Fact]
    public async Task Handle_MarksActiveFightFled()
    {
        // Arrange
        var fight = Builders.MakeFight(WorldId, _player.Id, [_player.Id, _enemy.Id]);
        _context.Fights.Add(fight);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        await _handler.Handle(
            new AbandonActiveFightCommand
            {
                WorldId = WorldId,
                PlayerId = _player.Id,
                Playtime = TimeSpan.FromHours(3),
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        var updatedFight = await verifyContext.Fights.FindAsync(
            [fight.Id],
            TestContext.Current.CancellationToken
        );
        Assert.NotNull(updatedFight!.CompletedAt);
        Assert.Equal(CombatOutcome.Fled, updatedFight.Outcome);
    }

    [Fact]
    public async Task Handle_AdvancesLastRegenPlaytime_ForSurvivingCombatantsOnly()
    {
        // Arrange
        var fight = Builders.MakeFight(WorldId, _player.Id, [_player.Id, _enemy.Id]);
        _context.Fights.Add(fight);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        await _handler.Handle(
            new AbandonActiveFightCommand
            {
                WorldId = WorldId,
                PlayerId = _player.Id,
                Playtime = TimeSpan.FromHours(3),
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        var player = await verifyContext.Creatures.FindAsync(
            [_player.Id],
            TestContext.Current.CancellationToken
        );
        var enemy = await verifyContext.Creatures.FindAsync(
            [_enemy.Id],
            TestContext.Current.CancellationToken
        );
        Assert.Equal(TimeSpan.FromHours(3), player!.LastRegenPlaytime);
        Assert.Equal(TimeSpan.Zero, enemy!.LastRegenPlaytime);
    }
}
