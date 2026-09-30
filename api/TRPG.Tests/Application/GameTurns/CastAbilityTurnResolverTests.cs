using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TRPG.Application.Combat;
using TRPG.Application.Configuration;
using TRPG.Application.GameTurns;
using TRPG.Application.WorldGeneration.Generators;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.GameTurns;

public sealed class CastAbilityTurnResolverTests(DatabaseFixture db)
    : IAsyncLifetime,
        IClassFixture<DatabaseFixture>
{
    private readonly Guid _worldId = Guid.NewGuid();
    private readonly Guid _stateId = Guid.NewGuid();
    private readonly Guid _locationId = Guid.NewGuid();

    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private CastAbilityTurnResolver _handler = null!;

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
        _handler = _serviceProvider.GetRequiredService<CastAbilityTurnResolver>();
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    private async Task<GameTurnSession> SeedKnight()
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
        _context.Creatures.Add(generated.Creature);
        _context.Items.AddRange(generated.Items);
        _context.CreatureSkills.AddRange(generated.Skills);
        return await SeedSession(generated.Creature.Id);
    }

    private async Task<GameTurnSession> SeedHealer()
    {
        var healer = Builders.MakeCreature(
            _worldId,
            locationId: _locationId,
            name: "Hero",
            currentHp: 20,
            currentMp: 8
        );
        _context.Creatures.Add(healer);
        _context.CreatureSkills.Add(
            Builders.MakeCreatureSkill(
                healer.Id,
                Skill.Restoration,
                level: 1,
                experience: 10_000,
                worldId: _worldId
            )
        );
        return await SeedSession(healer.Id);
    }

    private async Task<GameTurnSession> SeedSession(Guid playerId)
    {
        var session = Builders.MakeGameSession(_worldId, playerId);
        _context.Worlds.Add(Builders.MakeWorld(_worldId));
        _context.States.Add(Builders.MakeState(Guid.NewGuid(), worldId: _worldId, id: _stateId));
        _context.Locations.Add(Builders.MakeLocation(_worldId, _stateId, id: _locationId));
        _context.GameSessions.Add(session);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return new GameTurnSession(session.Id, _worldId, playerId);
    }

    private async Task<Creature> SeedTarget(
        string name,
        int? currentHp = null,
        Guid? locationId = null
    )
    {
        var target = Builders.MakeCreature(
            _worldId,
            creatureType: CreatureType.Beast,
            locationId: locationId ?? _locationId,
            name: name,
            currentHp: currentHp
        );
        _context.Creatures.Add(target);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return target;
    }

    [Fact]
    public void BuildNarrationPrompt_IncludesTheAbilityAndTarget()
    {
        // Arrange
        var fact = new AbilityCastFact("Mend", "Restores health.", "Bystander", false);

        // Act
        var prompt = CastAbilityTurnResolver.BuildNarrationPrompt(fact);

        // Assert
        Assert.Contains("Mend", prompt, StringComparison.Ordinal);
        Assert.Contains("Bystander", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Resolve_RepliesAbilityNotFound_WhenThePlayerLacksTheAbility()
    {
        // Arrange
        var session = await SeedKnight();
        var target = await SeedTarget("Wolf");

        // Act
        var prompt = await _handler.Resolve(
            session,
            target.Id,
            "Nonexistent",
            TestContext.Current.CancellationToken
        );

        // Assert
        var reply = Assert.IsType<GameTurnPrompt.Reply>(prompt);
        Assert.Equal("Ability Nonexistent not found", reply.Text);
    }

    [Fact]
    public async Task Resolve_NarratesWithoutTools_WhenASupportAbilityIsCast()
    {
        // Arrange
        var session = await SeedHealer();
        var target = await SeedTarget("Bystander", currentHp: 5);

        // Act
        var prompt = await _handler.Resolve(
            session,
            target.Id,
            "Mend",
            TestContext.Current.CancellationToken
        );

        // Assert
        var narrate = Assert.IsType<GameTurnPrompt.Narrate>(prompt);
        Assert.False(narrate.IncludeTools);
        Assert.Contains("Mend", narrate.Text, StringComparison.Ordinal);
        Assert.Contains("Bystander", narrate.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Resolve_ReturnsNone_WhenAnAttackAbilityOpensAnOngoingFight()
    {
        // Arrange
        var session = await SeedKnight();
        var target = await SeedTarget("Wolf");

        // Act
        var prompt = await _handler.Resolve(
            session,
            target.Id,
            "Strike",
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.IsType<GameTurnPrompt.None>(prompt);
    }

    [Fact]
    public async Task Resolve_NarratesTheConclusion_WhenAnAttackAbilityEndsTheFightInTheOpeningRound()
    {
        // Arrange
        var session = await SeedKnight();
        var target = await SeedTarget("Wolf", currentHp: 1);

        // Act
        var prompt = await _handler.Resolve(
            session,
            target.Id,
            "Strike",
            TestContext.Current.CancellationToken
        );

        // Assert
        var narrate = Assert.IsType<GameTurnPrompt.Narrate>(prompt);
        Assert.False(narrate.IncludeTools);
        Assert.Contains("Wolf", narrate.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Resolve_RepliesWithTheFailure_WhenTheTargetIsNotHere()
    {
        // Arrange
        var session = await SeedKnight();
        var faraway = await SeedTarget("Distant Wolf", locationId: Guid.NewGuid());

        // Act
        var prompt = await _handler.Resolve(
            session,
            faraway.Id,
            "Strike",
            TestContext.Current.CancellationToken
        );

        // Assert
        var reply = Assert.IsType<GameTurnPrompt.Reply>(prompt);
        Assert.NotEmpty(reply.Text);
    }
}
