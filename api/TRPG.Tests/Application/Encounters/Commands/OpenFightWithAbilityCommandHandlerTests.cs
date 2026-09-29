using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TRPG.Application.Combat;
using TRPG.Application.Configuration;
using TRPG.Application.Encounters.Commands;
using TRPG.Application.WorldGeneration.Generators;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Encounters.Commands;

public sealed class OpenFightWithAbilityCommandHandlerTests(DatabaseFixture db)
    : IAsyncLifetime,
        IClassFixture<DatabaseFixture>
{
    private readonly Guid _worldId = Guid.NewGuid();
    private readonly Guid _locationId = Guid.NewGuid();

    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private OpenFightWithAbilityCommandHandler _handler = null!;

    public ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .AddSingleton<IOptionsSnapshot<CombatOptions>>(
                new TestOptionsSnapshot<CombatOptions>(
                    new CombatOptions
                    {
                        MinHitChance = 1.0f,
                        MaxHitChance = 1.0f,
                        CritChancePerDexterityPoint = 0f,
                    }
                )
            )
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<OpenFightWithAbilityCommandHandler>();
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    private async Task<(Creature Player, Guid SessionId)> SeedPlayer()
    {
        var generated = Builders
            .MakeCreatureGenerator()
            .Generate(
                new CreatureGeneratorInput(
                    CreatureType.Human,
                    CreatureArchetype.For(Profession.Knight),
                    _worldId,
                    _locationId,
                    MinLevel: 1,
                    MaxLevel: 1
                )
            );
        generated.Creature.LocationId = _locationId;
        var session = Builders.MakeGameSession(_worldId, generated.Creature.Id);
        _context.Locations.Add(Builders.MakeLocation(_worldId, _locationId));
        _context.Creatures.Add(generated.Creature);
        _context.Items.AddRange(generated.Items);
        _context.CreatureSkills.AddRange(generated.Skills);
        _context.GameSessions.Add(session);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return (generated.Creature, session.Id);
    }

    private async Task<Creature> SeedEnemy(CreatureCondition condition = CreatureCondition.Awake)
    {
        var enemy = Builders.MakeCreature(
            _worldId,
            creatureType: CreatureType.Beast,
            locationId: _locationId,
            name: "Wolf",
            condition: condition
        );
        _context.Creatures.Add(enemy);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return enemy;
    }

    private OpenFightWithAbilityCommand MakeCommand(
        Creature player,
        Guid sessionId,
        Guid targetId,
        string abilityName
    ) =>
        new()
        {
            SessionId = sessionId,
            WorldId = _worldId,
            PlayerId = player.Id,
            TargetId = targetId,
            AbilityName = abilityName,
            GameTime = TestTime.Start,
        };

    [Fact]
    public async Task Handle_StartsAFightAndResolvesTheAbilityAsTheOpeningRound()
    {
        // Arrange
        var (player, sessionId) = await SeedPlayer();
        var enemy = await SeedEnemy();

        // Act
        await _handler.Handle(
            MakeCommand(player, sessionId, enemy.Id, "Strike"),
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        var fight = await verifyContext
            .Encounters.OfType<FightEncounter>()
            .SingleAsync(f => f.PlayerId == player.Id, TestContext.Current.CancellationToken);
        var hurtEnemy = await verifyContext.Creatures.SingleAsync(
            c => c.Id == enemy.Id,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(1, fight.RoundsResolved);
        Assert.True(hurtEnemy.CurrentHp < enemy.MaximumHp);
    }

    [Fact]
    public async Task Handle_StartsAFightWithASurpriseRound_WhenTheTargetIsAsleep()
    {
        // Arrange
        var (player, sessionId) = await SeedPlayer();
        var enemy = await SeedEnemy(CreatureCondition.Sleeping);

        // Act
        await _handler.Handle(
            MakeCommand(player, sessionId, enemy.Id, "Strike"),
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        var fight = await verifyContext
            .Encounters.OfType<FightEncounter>()
            .SingleAsync(f => f.PlayerId == player.Id, TestContext.Current.CancellationToken);
        Assert.True(fight.HasSurpriseRound);
    }

    [Fact]
    public async Task Handle_StartsNoFight_WhenTheAbilityIsUnknown()
    {
        // Arrange
        var (player, sessionId) = await SeedPlayer();
        var enemy = await SeedEnemy();

        // Act
        var attempt = () =>
            _handler.Handle(
                MakeCommand(player, sessionId, enemy.Id, "Nonexistent"),
                TestContext.Current.CancellationToken
            );

        // Assert
        await Assert.ThrowsAsync<InvalidOperationException>(attempt);
        await using var verifyContext = db.CreateContext();
        Assert.False(
            await verifyContext.Encounters.AnyAsync(
                e => e.PlayerId == player.Id,
                TestContext.Current.CancellationToken
            )
        );
    }

    [Fact]
    public async Task Handle_StartsNoFight_WhenTheTargetIsNotHere()
    {
        // Arrange
        var (player, sessionId) = await SeedPlayer();
        var faraway = Builders.MakeCreature(
            _worldId,
            creatureType: CreatureType.Beast,
            locationId: Guid.NewGuid(),
            name: "Distant Wolf"
        );
        _context.Creatures.Add(faraway);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var attempt = () =>
            _handler.Handle(
                MakeCommand(player, sessionId, faraway.Id, "Strike"),
                TestContext.Current.CancellationToken
            );

        // Assert
        await Assert.ThrowsAsync<InvalidOperationException>(attempt);
        await using var verifyContext = db.CreateContext();
        Assert.False(
            await verifyContext.Encounters.AnyAsync(
                e => e.PlayerId == player.Id,
                TestContext.Current.CancellationToken
            )
        );
    }
}
