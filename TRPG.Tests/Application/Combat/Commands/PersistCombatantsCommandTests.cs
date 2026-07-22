using TRPG.Application.Abilities;
using TRPG.Application.Combat.Commands;
using TRPG.Data;
using TRPG.Data.Models;
using TRPG.Tests.Helpers;
using ActiveBuff = TRPG.Application.Creatures.ActiveBuff;
using ActiveDot = TRPG.Application.Combat.ActiveDot;
using ActiveHot = TRPG.Application.Combat.ActiveHot;

namespace TRPG.Tests.Application.Combat.Commands;

[Collection("Database")]
public sealed class PersistCombatantsCommandTests(DatabaseFixture db) : IAsyncLifetime
{
    private TrpgDbContext _context = null!;
    private PersistCombatantsCommandHandler _handler = null!;
    private readonly Creature _creature = Builders.MakeCreature(
        currentHp: 100,
        currentAp: 20,
        currentMp: 10
    );

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _handler = new PersistCombatantsCommandHandler(_context);

        _context.Creatures.Add(_creature);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_PersistsHpApMp_ForAliveCombatant()
    {
        // Arrange
        var combatant = Builders.MakeCombatant(
            _creature.Id,
            currentHp: 30,
            currentAp: 5,
            currentMp: 3
        );

        // Act
        await _handler.Handle(
            new PersistCombatantsCommand { Combatants = [combatant] },
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        var updated = await verifyContext.Creatures.FindAsync(
            [_creature.Id],
            TestContext.Current.CancellationToken
        );
        Assert.Equal(30, updated!.CurrentHp);
        Assert.Equal(5, updated.CurrentAp);
        Assert.Equal(3, updated.CurrentMp);
        Assert.Equal(_creature.State, updated.State);
    }

    [Fact]
    public async Task Handle_MarksCreatureDead_WhenCombatantIsNotAlive()
    {
        // Arrange
        var combatant = Builders.MakeCombatant(
            _creature.Id,
            currentHp: 0,
            currentAp: 5,
            currentMp: 3
        );

        // Act
        await _handler.Handle(
            new PersistCombatantsCommand { Combatants = [combatant] },
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        var updated = await verifyContext.Creatures.FindAsync(
            [_creature.Id],
            TestContext.Current.CancellationToken
        );
        Assert.Equal(0, updated!.CurrentHp);
        Assert.Equal(CreatureState.Dead, updated.State);
    }

    [Fact]
    public async Task Handle_PersistsEveryCombatant_WhenGivenMultiple()
    {
        // Arrange
        var otherCreature = Builders.MakeCreature();
        _context.Creatures.Add(otherCreature);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var aliveCombatant = Builders.MakeCombatant(
            _creature.Id,
            currentHp: 30,
            currentAp: 5,
            currentMp: 3
        );
        var deadCombatant = Builders.MakeCombatant(
            otherCreature.Id,
            currentHp: 0,
            currentAp: 0,
            currentMp: 0
        );

        // Act
        await _handler.Handle(
            new PersistCombatantsCommand { Combatants = [aliveCombatant, deadCombatant] },
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        var updatedCreature = await verifyContext.Creatures.FindAsync(
            [_creature.Id],
            TestContext.Current.CancellationToken
        );
        var updatedOther = await verifyContext.Creatures.FindAsync(
            [otherCreature.Id],
            TestContext.Current.CancellationToken
        );
        Assert.Equal(30, updatedCreature!.CurrentHp);
        Assert.Equal(CreatureState.Dead, updatedOther!.State);
    }

    [Fact]
    public async Task Handle_PersistsActiveConditionsDotsHotsBuffs_OntoCreature()
    {
        // Arrange
        var combatant = Builders.MakeCombatant(_creature.Id);
        combatant.ActiveConditions[ConditionType.Poisoned] = 3;
        combatant.ActiveDots.Add(
            new ActiveDot
            {
                AbilityName = "Venom",
                Amount = 5,
                DamageType = DamageType.Poison,
                RemainingTurns = 3,
            }
        );
        combatant.ActiveHots.Add(
            new ActiveHot
            {
                AbilityName = "Regen",
                Amount = 4,
                RemainingTurns = 2,
            }
        );
        combatant.ActiveBuffs.Add(
            new ActiveBuff
            {
                Amount = 2,
                Attribute = AttributeName.Strength,
                RemainingTurns = 4,
                AmountType = AmountType.Flat,
            }
        );

        // Act
        await _handler.Handle(
            new PersistCombatantsCommand { Combatants = [combatant] },
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        var updated = await verifyContext.Creatures.FindAsync(
            [_creature.Id],
            TestContext.Current.CancellationToken
        );
        Assert.Equal(3, updated!.ActiveConditions["Poisoned"]);
        Assert.Single(updated.ActiveDots);
        Assert.Single(updated.ActiveHots);
        Assert.Single(updated.ActiveBuffs);
    }

    [Fact]
    public async Task Handle_UpdatesCachedMaximumHp_WhenBuffAppliesToMaximumHp()
    {
        // Arrange
        var baseMaximumHp = _creature.MaximumHp;
        var combatant = Builders.MakeCombatant(_creature.Id);
        combatant.ActiveBuffs.Add(
            new ActiveBuff
            {
                Amount = 50,
                Attribute = AttributeName.MaximumHp,
                RemainingTurns = 4,
                AmountType = AmountType.Flat,
            }
        );

        // Act
        await _handler.Handle(
            new PersistCombatantsCommand { Combatants = [combatant] },
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        var updated = await verifyContext.Creatures.FindAsync(
            [_creature.Id],
            TestContext.Current.CancellationToken
        );
        Assert.Equal(baseMaximumHp + 50, updated!.MaximumHp);
    }
}
