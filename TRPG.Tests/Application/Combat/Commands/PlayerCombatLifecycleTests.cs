using Microsoft.Extensions.Logging.Abstractions;
using TRPG.Application.Abilities;
using TRPG.Application.Combat;
using TRPG.Application.Combat.Commands;
using TRPG.Application.Configuration;
using TRPG.Application.Creatures.Commands;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.GameSessions.Queries;
using TRPG.Application.Inventory.Queries;
using TRPG.Application.WeaponProficiency.Queries;
using TRPG.Application.Worlds.Generators;
using TRPG.Data;
using TRPG.Data.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Combat.Commands;

[Collection("Database")]
public sealed class PlayerCombatLifecycleTests(DatabaseFixture db) : IAsyncLifetime
{
    private TrpgDbContext _context = null!;

    public ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task PlayerLifecycle_KeepsMaximumHpStable_FromCreationThroughCombat()
    {
        // Arrange — generate and persist a player exactly like real character creation does
        var worldId = Guid.NewGuid();
        var stateId = Guid.NewGuid();
        var abilityDefinitions = AbilityDefinitions.Create();
        var generator = Builders.MakeCreatureGenerator();
        var playerResult = generator.Generate(
            new CreatureGeneratorInput(
                CreatureType.Human,
                CreatureArchetype.For(Profession.Knight),
                worldId,
                stateId,
                stateId,
                MinLevel: 1,
                MaxLevel: 1
            )
        );

        _context.Creatures.Add(playerResult.Creature);
        _context.Items.AddRange(playerResult.Items);
        _context.InventoryItems.AddRange(playerResult.InventoryItems);
        _context.CreatureSkills.AddRange(playerResult.Skills);
        _context.CreatureAbilities.AddRange(playerResult.Abilities);

        var enemy = Builders.MakeCreature(
            worldId,
            creatureType: CreatureType.Beast,
            stateId: stateId
        );
        var session = Builders.MakeGameSession(worldId, playerResult.Creature.Id);
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

        // Act — start a fight exactly like StartFightTool does on the first attack
        var startFight = new StartFightCommandHandler(
            _context,
            new GetCreatureByIdQueryHandler(_context),
            new GetAllNearbyCreaturesQueryHandler(_context),
            new GetInventoryByCreatureIdQueryHandler(_context),
            new GetAllWeaponProficienciesQueryHandler(_context),
            new GetCreatureAbilitiesQueryHandler(_context),
            new ApplyPassiveRegenCommandHandler(
                _context,
                new TestOptionsSnapshot<CreatureRegenOptions>(new CreatureRegenOptions()),
                new GetPlaytimeQueryHandler(_context, NullLogger<GetPlaytimeQueryHandler>.Instance)
            ),
            abilityDefinitions
        );

        var combatants = await startFight.Handle(
            new StartFightCommand
            {
                SessionId = session.Id,
                WorldId = worldId,
                PlayerId = playerResult.Creature.Id,
                TargetName = enemy.Name,
            },
            TestContext.Current.CancellationToken
        );

        // Assert — entering combat must not change Maximum or Current HP by itself
        var playerCombatant = combatants.Single(c => c.IsPlayer);
        Assert.Equal(maximumHpAtCreation, playerCombatant.MaximumHp);
        Assert.Equal(currentHpAtCreation, playerCombatant.CurrentHp);

        // Act — run one full combat round with guaranteed hits both ways
        var alwaysHit = new TestOptionsSnapshot<CombatOptions>(
            new CombatOptions { MinHitChance = 1.0f, MaxHitChance = 1.0f }
        );
        var engine = new CombatEngine(
            alwaysHit,
            new HitCalculator(alwaysHit),
            new DamageCalculator(alwaysHit)
        );
        var resolution = PlayerActionResolver.Resolve(
            combatants,
            new UseAbility(enemy.Id, "Strike")
        );
        var resolved = Assert.IsType<ActionResolved>(resolution);
        engine.ProcessRound(combatants, resolved.Action);

        await new PersistCombatantsCommandHandler(_context).Handle(
            new PersistCombatantsCommand { Combatants = combatants },
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
