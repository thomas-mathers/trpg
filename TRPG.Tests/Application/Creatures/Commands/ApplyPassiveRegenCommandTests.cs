using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using TRPG.Application.Configuration;
using TRPG.Application.Creatures.Commands;
using TRPG.Application.GameSessions;
using TRPG.Application.GameSessions.Queries;
using TRPG.Application.Inventory.Commands;
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
    private GameSession _session = null!;
    private Guid _sessionId;
    private ApplyPassiveRegenCommandHandler _handler = null!;
    private readonly Creature _creature = Builders.MakeCreature(
        currentHp: 0,
        currentAp: 0,
        currentMp: 0
    );

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();

        _session = Builders.MakeGameSession(_creature.WorldId, _creature.Id);
        _context.Creatures.Add(_creature);
        _context.GameSessions.Add(_session);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        _sessionId = _session.Id;

        _handler = new ApplyPassiveRegenCommandHandler(
            _context,
            new TestOptionsSnapshot<CreatureRegenOptions>(RegenOptions),
            new GetPlaytimeQueryHandler(_context, NullLogger<GetPlaytimeQueryHandler>.Instance)
        );
    }

    public async ValueTask DisposeAsync()
    {
        await _context.DisposeAsync();
    }

    private async Task SetPlaytime(TimeSpan playtime)
    {
        _session.Playtime = playtime;
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Handle_RegeneratesHpApMp_ProportionalToElapsedInGameHours()
    {
        // Arrange
        await SetPlaytime(GameClock.RealTimePerInGameHour);

        // Act
        await _handler.Handle(
            new ApplyPassiveRegenCommand { SessionId = _sessionId, CreatureIds = [_creature.Id] },
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
    public async Task Handle_ReturnsDetachedCreatures_ReflectingRegeneratedValues()
    {
        // Arrange
        await SetPlaytime(GameClock.RealTimePerInGameHour);

        // Act
        var result = await _handler.Handle(
            new ApplyPassiveRegenCommand { SessionId = _sessionId, CreatureIds = [_creature.Id] },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(7, result[_creature.Id].CurrentHp);
        Assert.Equal(EntityState.Detached, _context.Entry(result[_creature.Id]).State);
    }

    [Fact]
    public async Task Handle_ClampsAtMaximum_WhenElapsedTimeExceedsFullRegen()
    {
        // Arrange
        await SetPlaytime(TimeSpan.FromHours(100 / 12.0));

        // Act
        await _handler.Handle(
            new ApplyPassiveRegenCommand { SessionId = _sessionId, CreatureIds = [_creature.Id] },
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        var updated = await verifyContext.Creatures.FindAsync(
            [_creature.Id],
            TestContext.Current.CancellationToken
        );
        Assert.Equal(_creature.MaximumHp, updated!.CurrentHp);
        Assert.Equal(_creature.MaximumAp, updated.CurrentAp);
        Assert.Equal(_creature.MaximumMp, updated.CurrentMp);
    }

    [Fact]
    public async Task Handle_DoesNothing_WhenCreatureIsDead()
    {
        // Arrange
        _creature.State = CreatureState.Dead;
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        await SetPlaytime(TimeSpan.FromHours(100 / 12.0));

        // Act
        await _handler.Handle(
            new ApplyPassiveRegenCommand { SessionId = _sessionId, CreatureIds = [_creature.Id] },
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
        await SetPlaytime(TimeSpan.FromHours(1));

        // Act
        await _handler.Handle(
            new ApplyPassiveRegenCommand { SessionId = _sessionId, CreatureIds = [_creature.Id] },
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
        var baseMaximumHp = _creature.MaximumHp;
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
                WorldId = _creature.WorldId,
            }
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        await new EquipInventoryItemCommandHandler(_context).Handle(
            new EquipInventoryItemCommand
            {
                CreatureId = _creature.Id,
                ItemId = gear.Id,
                Slot = EquipmentSlot.Chest,
            },
            TestContext.Current.CancellationToken
        );

        await SetPlaytime(GameClock.RealTimePerInGameHour);

        var fullHpRegenOptions = new CreatureRegenOptions { HpRegenPercentPerHour = 1.0f };
        var handler = new ApplyPassiveRegenCommandHandler(
            _context,
            new TestOptionsSnapshot<CreatureRegenOptions>(fullHpRegenOptions),
            new GetPlaytimeQueryHandler(_context, NullLogger<GetPlaytimeQueryHandler>.Instance)
        );

        // Act
        await handler.Handle(
            new ApplyPassiveRegenCommand { SessionId = _sessionId, CreatureIds = [_creature.Id] },
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        var updated = await verifyContext.Creatures.FindAsync(
            [_creature.Id],
            TestContext.Current.CancellationToken
        );
        Assert.Equal(baseMaximumHp + 50, updated!.CurrentHp);
    }
}
