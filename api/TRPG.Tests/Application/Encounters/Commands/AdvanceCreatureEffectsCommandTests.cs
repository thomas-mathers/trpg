using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Abilities;
using TRPG.Application.Encounters.Commands;
using TRPG.Data;
using TRPG.Domain;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Encounters.Commands;

public sealed class AdvanceCreatureEffectsCommandTests(DatabaseFixture db)
    : IAsyncLifetime,
        IClassFixture<DatabaseFixture>
{
    private static readonly GameInstant OneRoundIn = GameClock.Epoch + CombatTiming.Round;

    private readonly Guid _worldId = Guid.NewGuid();

    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private AdvanceCreatureEffectsCommandHandler _handler = null!;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<AdvanceCreatureEffectsCommandHandler>();

        _context.Worlds.Add(Builders.MakeWorld(_worldId, OneRoundIn));
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    private async Task<Creature> SeedCreature(
        int currentHp = 50,
        ActiveDot? dot = null,
        ActiveHot? hot = null
    )
    {
        var creature = Builders.MakeCreature(_worldId, currentHp: currentHp);
        if (dot != null)
        {
            creature.ActiveDots = [dot];
        }
        if (hot != null)
        {
            creature.ActiveHots = [hot];
        }
        _context.Creatures.Add(creature);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return creature;
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

    private static ActiveHot MakeHot(int amount) =>
        new()
        {
            AbilityName = "Mend",
            Amount = amount,
            NextTickAt = OneRoundIn,
            ExpiresAt = OneRoundIn + CombatTiming.Round * 10,
        };

    private Task<IReadOnlyCollection<TRPG.Application.Creatures.Results.CreatureVitals>> Advance(
        GameInstant gameTime,
        params Creature[] creatures
    ) =>
        _handler.Handle(
            new AdvanceCreatureEffectsCommand
            {
                WorldId = _worldId,
                CreatureIds = creatures.Select(creature => creature.Id).ToArray(),
                GameTime = gameTime,
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

    [Fact]
    public async Task Handle_AppliesTheOwedDotTickAndReturnsTheNewVitals()
    {
        // Arrange
        var creature = await SeedCreature(currentHp: 50, dot: MakeDot(amount: 5));

        // Act
        var vitals = await Advance(OneRoundIn, creature);

        // Assert
        var reloaded = await Reload(creature.Id);
        Assert.True(reloaded.CurrentHp < 50);
        Assert.Equal(reloaded.CurrentHp, Assert.Single(vitals).CurrentHp);
    }

    [Fact]
    public async Task Handle_HealsTheCreature_WhenAHotTickIsOwed()
    {
        // Arrange
        var creature = await SeedCreature(currentHp: 10, hot: MakeHot(amount: 5));

        // Act
        await Advance(OneRoundIn, creature);

        // Assert
        Assert.True((await Reload(creature.Id)).CurrentHp > 10);
    }

    [Fact]
    public async Task Handle_KillsTheCreature_WhenTheTickIsLethal()
    {
        // Arrange
        var creature = await SeedCreature(currentHp: 5, dot: MakeDot(amount: 1000));

        // Act
        await Advance(OneRoundIn, creature);

        // Assert
        Assert.Equal(CreatureCondition.Dead, (await Reload(creature.Id)).Condition);
    }

    [Fact]
    public async Task Handle_ClearsTheExpiredEffect_WhenItRanOutWithoutADueTick()
    {
        // Arrange
        var creature = await SeedCreature(dot: MakeDot(amount: 5));

        // Act
        var vitals = await Advance(GameClock.Epoch + TimeSpan.FromHours(1), creature);

        // Assert
        Assert.Empty((await Reload(creature.Id)).ActiveDots);
        Assert.NotEmpty(vitals);
    }

    [Fact]
    public async Task Handle_ReturnsNothingAndChangesNothing_WhenNoTickIsDue()
    {
        // Arrange
        var creature = await SeedCreature(currentHp: 50, dot: MakeDot(amount: 5));

        // Act
        var vitals = await Advance(GameClock.Epoch, creature);

        // Assert
        Assert.Empty(vitals);
        Assert.Equal(50, (await Reload(creature.Id)).CurrentHp);
    }

    [Fact]
    public async Task Handle_ReturnsNothing_WhenTheCreatureHasNoEffects()
    {
        // Arrange
        var creature = await SeedCreature();

        // Act
        var vitals = await Advance(OneRoundIn, creature);

        // Assert
        Assert.Empty(vitals);
    }
}
