using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Abilities;
using TRPG.Application.Combat.Events;
using TRPG.Application.Common.Events;
using TRPG.Application.Creatures.Events;
using TRPG.Application.Encounters.Events;
using TRPG.Application.LocationSimulation;
using TRPG.Application.LocationSimulation.Commands;
using TRPG.Data;
using TRPG.Domain;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.LocationSimulation.Commands;

public sealed class SyncActiveLocationEffectsCommandTests(DatabaseFixture db)
    : IAsyncLifetime,
        IClassFixture<DatabaseFixture>
{
    private static readonly GameInstant OneRoundIn = GameClock.Epoch + CombatTiming.Round;

    private readonly Guid _worldId = Guid.NewGuid();
    private readonly Guid _locationId = Guid.NewGuid();

    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private SyncActiveLocationEffectsCommandHandler _handler = null!;
    private Creature _player = null!;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<SyncActiveLocationEffectsCommandHandler>();

        _player = Builders.MakeCreature(_worldId, locationId: _locationId, currentHp: 50);
        _context.Worlds.Add(Builders.MakeWorld(_worldId, OneRoundIn));
        _context.Creatures.Add(_player);
        _context.GameSessions.Add(Builders.MakeGameSession(_worldId, _player.Id));
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    private static ActiveDot MakeDot(int amount) =>
        new()
        {
            AbilityName = "Ignite",
            Amount = amount,
            DamageType = nameof(DamageType.Fire),
            NextTickAt = OneRoundIn,
            ExpiresAt = OneRoundIn + CombatTiming.Round * 10,
        };

    private async Task<Creature> SeedBurning(Guid? locationId = null, int amount = 5)
    {
        var creature = Builders.MakeCreature(
            _worldId,
            locationId: locationId ?? _locationId,
            currentHp: 50
        );
        creature.ActiveDots = [MakeDot(amount)];
        _context.Creatures.Add(creature);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return creature;
    }

    private async Task BurnThePlayer(int amount)
    {
        _player.ActiveDots = [MakeDot(amount)];
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private Task Sync() =>
        _handler.Handle(
            new SyncActiveLocationEffectsCommand
            {
                WorldId = _worldId,
                GameTime = OneRoundIn,
                Players = [new ActiveLocationPlayer(_locationId, _player.Id, PlayerLevel: 1)],
            },
            TestContext.Current.CancellationToken
        );

    private async Task<Creature> Reload(Guid creatureId)
    {
        await using var verifyContext = db.CreateContext();
        return await verifyContext
            .Creatures.AsNoTracking()
            .SingleAsync(
                creature => creature.Id == creatureId,
                TestContext.Current.CancellationToken
            );
    }

    private IReadOnlyList<T> Published<T>()
        where T : GameClientEvent =>
        _serviceProvider
            .GetRequiredService<TestGameClientEventSink>()
            .EnqueuedEvents.OfType<T>()
            .ToArray();

    [Fact]
    public async Task Handle_TicksThePlayerOutsideCombatAndPublishesVitals()
    {
        // Arrange
        await BurnThePlayer(amount: 5);

        // Act
        await Sync();

        // Assert
        var reloaded = await Reload(_player.Id);
        var vitals = Assert.Single(Published<PlayerVitalsChangedEvent>());
        Assert.True(reloaded.CurrentHp < 50);
        Assert.Equal(reloaded.CurrentHp, vitals.Vitals.CurrentHp);
    }

    [Fact]
    public async Task Handle_KillsThePlayer_WhenTheTickIsLethal()
    {
        // Arrange
        await BurnThePlayer(amount: 1000);

        // Act
        await Sync();

        // Assert
        Assert.Equal(CreatureCondition.Dead, (await Reload(_player.Id)).Condition);
    }

    [Fact]
    public async Task Handle_TicksBystandersWithoutPublishingTheirVitals()
    {
        // Arrange
        var bystander = await SeedBurning();

        // Act
        await Sync();

        // Assert
        Assert.True((await Reload(bystander.Id)).CurrentHp < 50);
        Assert.Empty(Published<PlayerVitalsChangedEvent>());
    }

    [Fact]
    public async Task Handle_LeavesCreaturesAtOtherLocationsAlone()
    {
        // Arrange
        var distant = await SeedBurning(locationId: Guid.NewGuid());

        // Act
        await Sync();

        // Assert
        Assert.Equal(50, (await Reload(distant.Id)).CurrentHp);
    }

    [Fact]
    public async Task Handle_TicksAFightThroughTheFightCommand_NotTheOutOfCombatPath()
    {
        // Arrange
        var enemy = await SeedBurning(amount: 1000);
        _context.Encounters.Add(
            new FightEncounter
            {
                WorldId = _worldId,
                PlayerId = _player.Id,
                LocationId = _locationId,
                CombatantIds = [_player.Id, enemy.Id],
            }
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        await Sync();

        // Assert
        var update = Assert.Single(Published<CombatUpdatedEvent>());
        Assert.Equal(CombatOutcome.Victory, update.Outcome);
        Assert.Empty(Published<PlayerVitalsChangedEvent>());
    }
}
