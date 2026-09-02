using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TRPG.Application.Combat;
using TRPG.Application.Combat.Commands;
using TRPG.Application.Combat.Mappers;
using TRPG.Application.Configuration;
using TRPG.Application.Creatures.Commands;
using TRPG.Application.WorldGeneration.Generators;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Combat.Commands;

[Collection("Database")]
public sealed class PlayerCombatLifecycleTests(DatabaseFixture db) : IAsyncLifetime
{
    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private StartFightCommandHandler _startFight = null!;
    private CombatEngine _combatEngine = null!;
    private PersistCombatantsCommandHandler _persistCombatants = null!;

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
                        // A real random crit roll would otherwise make this test's exact HP
                        // assertions flaky.
                        CritChancePerDexterityPoint = 0f,
                    }
                )
            )
            .BuildServiceProvider();

        _startFight = _serviceProvider.GetRequiredService<StartFightCommandHandler>();
        _combatEngine = _serviceProvider.GetRequiredService<CombatEngine>();
        _persistCombatants = _serviceProvider.GetRequiredService<PersistCombatantsCommandHandler>();

        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task PlayerLifecycle_KeepsMaximumHpStable_FromCreationThroughCombat()
    {
        // Arrange — generate and persist a player exactly like real character creation does
        var worldId = Guid.NewGuid();
        var birthLocationId = Guid.NewGuid();
        var generator = Builders.MakeCreatureGenerator();
        var playerResult = generator.Generate(
            new CreatureGeneratorInput(
                CreatureType.Human,
                CreatureArchetype.For(Profession.Knight),
                worldId,
                birthLocationId,
                MinLevel: 1,
                MaxLevel: 1
            )
        );

        var location = Builders.MakeLocation(worldId, birthLocationId);
        playerResult.Creature.LocationId = location.Id;
        _context.Creatures.Add(playerResult.Creature);
        _context.Items.AddRange(playerResult.Items);
        _context.CreatureSkills.AddRange(playerResult.Skills);

        var enemy = Builders.MakeCreature(
            worldId,
            creatureType: CreatureType.Beast,
            locationId: location.Id
        );
        var session = Builders.MakeGameSession(worldId, playerResult.Creature.Id);
        _context.Locations.Add(location);
        _context.Creatures.Add(enemy);
        _context.GameSessions.Add(session);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert — freshly created character starts at full, cache-consistent resources
        await using (var freshContext = db.CreateContext())
        {
            var freshPlayer = await freshContext.Creatures.FindAsync(
                [playerResult.Creature.Id],
                TestContext.Current.CancellationToken
            );
            Assert.Equal(freshPlayer!.MaximumHp, freshPlayer.CurrentHp);
            Assert.True(freshPlayer.MaximumHp > 0);
        }

        var maximumHpAtCreation = playerResult.Creature.MaximumHp;
        var currentHpAtCreation = playerResult.Creature.CurrentHp;

        // Act — start a fight exactly like StartFightTool does before the player chooses an action
        await _startFight.Handle(
            new StartFightCommand
            {
                SessionId = session.Id,
                WorldId = worldId,
                PlayerId = playerResult.Creature.Id,
                EnemyCreatureIds = [enemy.Id],
            },
            TestContext.Current.CancellationToken
        );

        // Assert — entering combat must not change Maximum or Current HP by itself
        var combatants = await _serviceProvider
            .GetRequiredService<ActiveFightCombatantLoader>()
            .Load(playerResult.Creature.Id, TestContext.Current.CancellationToken);
        var playerCombatant = combatants.Single(c => c.IsPlayer);
        Assert.Equal(maximumHpAtCreation, playerCombatant.MaximumHp);
        Assert.Equal(currentHpAtCreation, playerCombatant.CurrentHp);

        // Act — run one full combat round with guaranteed hits both ways
        var resolution = new PlayerCombatActionResolver(combatants).Resolve(
            new UseAbilityAction(enemy.Id, "Strike")
        );
        Assert.NotNull(resolution.Result);
        _combatEngine.ProcessRound(combatants, resolution.Result);

        await _persistCombatants.Handle(
            new PersistCombatantsCommand
            {
                Updates = combatants
                    .Select(combatant => combatant.ToCreatureCombatStateUpdate())
                    .ToArray(),
            },
            TestContext.Current.CancellationToken
        );

        // Assert — MaximumHp is unchanged by combat; CurrentHp only ever goes down from damage
        await using var verifyContext = db.CreateContext();
        var updatedPlayer = await verifyContext.Creatures.FindAsync(
            [playerResult.Creature.Id],
            TestContext.Current.CancellationToken
        );
        Assert.Equal(maximumHpAtCreation, updatedPlayer!.MaximumHp);
        Assert.True(updatedPlayer.CurrentHp <= currentHpAtCreation);
    }
}
