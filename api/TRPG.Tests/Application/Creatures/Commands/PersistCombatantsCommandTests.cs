using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Creatures.Commands;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Creatures.Commands;

[Collection("Database")]
public sealed class PersistCombatantsCommandTests(DatabaseFixture db) : IAsyncLifetime
{
    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private PersistCombatantsCommandHandler _handler = null!;
    private readonly Creature _creature = Builders.MakeCreature(
        currentHp: 100,
        currentAp: 20,
        currentMp: 10
    );

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<PersistCombatantsCommandHandler>();

        _context.Creatures.Add(_creature);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    private static CreatureCombatStateUpdate MakeUpdate(
        Guid creatureId,
        int currentHp = 100,
        int currentAp = 20,
        int currentMp = 10,
        bool isAlive = true,
        IReadOnlyDictionary<string, int>? activeConditions = null,
        IReadOnlyList<ActiveDot>? activeDots = null,
        IReadOnlyList<ActiveHot>? activeHots = null,
        IReadOnlyList<ActiveBuff>? activeBuffs = null
    ) =>
        new(
            creatureId,
            currentHp,
            currentAp,
            currentMp,
            isAlive,
            activeConditions ?? new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            activeDots ?? [],
            activeHots ?? [],
            activeBuffs ?? []
        );

    [Fact]
    public async Task Handle_PersistsHpApMp_ForAliveCombatant()
    {
        // Arrange
        var update = MakeUpdate(_creature.Id, currentHp: 30, currentAp: 5, currentMp: 3);

        // Act
        await _handler.Handle(
            new PersistCombatantsCommand { Updates = [update] },
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
        var update = MakeUpdate(
            _creature.Id,
            currentHp: 0,
            currentAp: 5,
            currentMp: 3,
            isAlive: false
        );

        // Act
        await _handler.Handle(
            new PersistCombatantsCommand { Updates = [update] },
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

        var aliveUpdate = MakeUpdate(_creature.Id, currentHp: 30, currentAp: 5, currentMp: 3);
        var deadUpdate = MakeUpdate(
            otherCreature.Id,
            currentHp: 0,
            currentAp: 0,
            currentMp: 0,
            isAlive: false
        );

        // Act
        await _handler.Handle(
            new PersistCombatantsCommand { Updates = [aliveUpdate, deadUpdate] },
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
        var update = MakeUpdate(
            _creature.Id,
            activeConditions: new Dictionary<string, int> { ["Poisoned"] = 3 },
            activeDots:
            [
                new ActiveDot
                {
                    AbilityName = "Venom",
                    Amount = 5,
                    DamageType = "Poison",
                    RemainingTurns = 3,
                },
            ],
            activeHots:
            [
                new ActiveHot
                {
                    AbilityName = "Regen",
                    Amount = 4,
                    RemainingTurns = 2,
                },
            ],
            activeBuffs:
            [
                new ActiveBuff
                {
                    Amount = 2,
                    Attribute = "Strength",
                    RemainingTurns = 4,
                    AmountType = "Flat",
                },
            ]
        );

        // Act
        await _handler.Handle(
            new PersistCombatantsCommand { Updates = [update] },
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
        var update = MakeUpdate(
            _creature.Id,
            activeBuffs:
            [
                new ActiveBuff
                {
                    Amount = 50,
                    Attribute = "MaximumHp",
                    RemainingTurns = 4,
                    AmountType = "Flat",
                },
            ]
        );

        // Act
        await _handler.Handle(
            new PersistCombatantsCommand { Updates = [update] },
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
