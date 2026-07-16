using TRPG.Application.Abilities;
using TRPG.Application.Combat;
using TRPG.Application.Combat.Commands;
using TRPG.Data;
using TRPG.Data.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Combat.Commands;

[Collection("Database")]
public sealed class PersistCombatantResourcesCommandTests(DatabaseFixture db) : IAsyncLifetime
{
    private TrpgDbContext _context = null!;
    private Creature _creature = null!;
    private PersistCombatantResourcesCommandHandler _handler = null!;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _handler = new PersistCombatantResourcesCommandHandler(_context);

        _creature = Builders.MakeCreature(currentHp: 100, currentAp: 20, currentMp: 10);
        _context.Creatures.Add(_creature);
        await _context.SaveChangesAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _context.DisposeAsync();
    }

    private static CombatantState MakeCombatantState(
        Guid id,
        int currentHp,
        int currentAp,
        int currentMp,
        bool isAlive
    ) =>
        new(
            Id: id,
            Name: "Test Combatant",
            IsPlayer: true,
            CurrentHp: currentHp,
            MaximumHp: 100,
            CurrentAp: currentAp,
            CurrentMp: currentMp,
            IsAlive: isAlive,
            Abilities: [],
            ActiveConditions: new Dictionary<ConditionType, int>()
        );

    [Fact]
    public async Task Handle_PersistsHpApMp_ForAliveCombatant()
    {
        // Act
        await _handler.Handle(
            new PersistCombatantResourcesCommand
            {
                Combatants = [MakeCombatantState(_creature.Id, 42, 5, 3, isAlive: true)],
                Playtime = TimeSpan.FromHours(1),
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        var updated = await verifyContext.Creatures.FindAsync(
            [_creature.Id],
            TestContext.Current.CancellationToken
        );
        Assert.Equal(42, updated!.CurrentHp);
        Assert.Equal(5, updated.CurrentAp);
        Assert.Equal(3, updated.CurrentMp);
        Assert.Equal(TimeSpan.FromHours(1), updated.LastRegenPlaytime);
        Assert.Equal(_creature.State, updated.State);
    }

    [Fact]
    public async Task Handle_MarksCreatureDead_WhenCombatantIsNotAlive()
    {
        // Act
        await _handler.Handle(
            new PersistCombatantResourcesCommand
            {
                Combatants = [MakeCombatantState(_creature.Id, 0, 5, 3, isAlive: false)],
                Playtime = TimeSpan.FromHours(1),
            },
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
    public async Task Handle_DoesNotAdvanceLastRegenPlaytime_WhenCombatantIsNotAlive()
    {
        // Act
        await _handler.Handle(
            new PersistCombatantResourcesCommand
            {
                Combatants = [MakeCombatantState(_creature.Id, 0, 5, 3, isAlive: false)],
                Playtime = TimeSpan.FromHours(1),
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        var updated = await verifyContext.Creatures.FindAsync(
            [_creature.Id],
            TestContext.Current.CancellationToken
        );
        Assert.Equal(TimeSpan.Zero, updated!.LastRegenPlaytime);
    }

    [Fact]
    public async Task Handle_PersistsEveryCombatant_WhenGivenMultiple()
    {
        // Arrange
        var otherCreature = Builders.MakeCreature();
        _context.Creatures.Add(otherCreature);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        await _handler.Handle(
            new PersistCombatantResourcesCommand
            {
                Combatants =
                [
                    MakeCombatantState(_creature.Id, 42, 5, 3, isAlive: true),
                    MakeCombatantState(otherCreature.Id, 0, 0, 0, isAlive: false),
                ],
                Playtime = TimeSpan.FromHours(1),
            },
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
        Assert.Equal(42, updatedCreature!.CurrentHp);
        Assert.Equal(CreatureState.Dead, updatedOther!.State);
    }
}
