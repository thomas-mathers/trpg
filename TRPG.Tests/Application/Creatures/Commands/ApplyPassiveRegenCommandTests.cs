using TRPG.Application.Configuration;
using TRPG.Application.Creatures.Commands;
using TRPG.Application.GameSessions;
using TRPG.Data;
using TRPG.Data.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Creatures.Commands;

[Collection("Database")]
public sealed class ApplyPassiveRegenCommandTests(DatabaseFixture db) : IAsyncLifetime
{
    private static readonly CreatureRegenOptions RegenOptions = new()
    {
        HpRegenPercentPerHour = 0.2f,
        ApRegenPercentPerHour = 0.25f,
        MpRegenPercentPerHour = 0.25f,
    };

    private TrpgDbContext _context = null!;
    private Creature _creature = null!;
    private ApplyPassiveRegenCommandHandler _handler = null!;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _handler = new ApplyPassiveRegenCommandHandler(
            _context,
            new TestOptionsSnapshot<CreatureRegenOptions>(RegenOptions)
        );

        _creature = Builders.MakeCreature(currentHp: 0, currentAp: 0, currentMp: 0);
        _context.Creatures.Add(_creature);
        await _context.SaveChangesAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_RegeneratesHpApMp_ProportionalToElapsedInGameHours()
    {
        // Act
        await _handler.Handle(
            new ApplyPassiveRegenCommand
            {
                CreatureIds = [_creature.Id],
                Playtime = GameClock.RealTimePerInGameHour,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        var updated = await verifyContext.Creatures.FindAsync(
            [_creature.Id],
            TestContext.Current.CancellationToken
        );
        Assert.Equal(7, updated!.CurrentHp);
        Assert.Equal(3, updated.CurrentAp);
        Assert.Equal(2, updated.CurrentMp);
        Assert.Equal(GameClock.RealTimePerInGameHour, updated.LastRegenPlaytime);
    }

    [Fact]
    public async Task Handle_ClampsAtMaximum_WhenElapsedTimeExceedsFullRegen()
    {
        // Act
        await _handler.Handle(
            new ApplyPassiveRegenCommand
            {
                CreatureIds = [_creature.Id],
                Playtime = TimeSpan.FromHours(100 / 12.0),
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        var updated = await verifyContext.Creatures.FindAsync(
            [_creature.Id],
            TestContext.Current.CancellationToken
        );
        Assert.Equal(_creature.Attributes.MaximumHp, updated!.CurrentHp);
        Assert.Equal(_creature.Attributes.MaximumAp, updated.CurrentAp);
        Assert.Equal(_creature.Attributes.MaximumMp, updated.CurrentMp);
    }

    [Fact]
    public async Task Handle_DoesNothing_WhenCreatureIsDead()
    {
        // Arrange
        _creature.State = CreatureState.Dead;
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        await _handler.Handle(
            new ApplyPassiveRegenCommand
            {
                CreatureIds = [_creature.Id],
                Playtime = TimeSpan.FromHours(100 / 12.0),
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
        Assert.Equal(TimeSpan.Zero, updated.LastRegenPlaytime);
    }

    [Fact]
    public async Task Handle_DoesNothing_WhenElapsedTimeIsZeroOrNegative()
    {
        // Arrange
        _creature.LastRegenPlaytime = TimeSpan.FromHours(1);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        await _handler.Handle(
            new ApplyPassiveRegenCommand
            {
                CreatureIds = [_creature.Id],
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
        Assert.Equal(TimeSpan.FromHours(1), updated.LastRegenPlaytime);
    }

    [Fact]
    public async Task Handle_RegeneratesTowardEffectiveMaximum_WhenGearIsEquipped()
    {
        // Arrange
        var gear = Builders.MakeArmorItem(worldId: _creature.WorldId);
        gear.Modifiers.Add(
            new AttributeModifier
            {
                Attribute = AttributeName.MaximumHp,
                Amount = 50,
                AmountType = AmountType.Flat,
            }
        );
        _context.Items.Add(gear);
        _context.InventoryItems.Add(
            new InventoryItem
            {
                CreatureId = _creature.Id,
                ItemId = gear.Id,
                Quantity = 1,
                Index = 0,
                EquippedSlot = EquipmentSlot.Chest,
                WorldId = _creature.WorldId,
            }
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var fullHpRegenOptions = new CreatureRegenOptions { HpRegenPercentPerHour = 1.0f };
        var handler = new ApplyPassiveRegenCommandHandler(
            _context,
            new TestOptionsSnapshot<CreatureRegenOptions>(fullHpRegenOptions)
        );

        // Act
        await handler.Handle(
            new ApplyPassiveRegenCommand
            {
                CreatureIds = [_creature.Id],
                Playtime = GameClock.RealTimePerInGameHour,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        var updated = await verifyContext.Creatures.FindAsync(
            [_creature.Id],
            TestContext.Current.CancellationToken
        );
        Assert.Equal(_creature.Attributes.MaximumHp + 50, updated!.CurrentHp);
    }
}
