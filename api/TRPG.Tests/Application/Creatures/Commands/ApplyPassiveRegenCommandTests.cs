using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TRPG.Application.Configuration;
using TRPG.Application.Creatures.Commands;
using TRPG.Application.GameSessions;
using TRPG.Application.Inventory.Commands;
using TRPG.Data;
using TRPG.Domain.Models;
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
    private ServiceProvider _serviceProvider = null!;
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

        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .AddSingleton<IOptionsSnapshot<CreatureRegenOptions>>(
                new TestOptionsSnapshot<CreatureRegenOptions>(RegenOptions)
            )
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<ApplyPassiveRegenCommandHandler>();
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
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
        var gear = Builders.MakeArmorItem(
            worldId: _creature.WorldId,
            modifiers:
            [
                new AttributeModifier
                {
                    Attribute = AttributeName.MaximumHp,
                    Amount = 50,
                    AmountType = AmountType.Flat,
                },
            ]
        );
        gear.Quantity = 1;
        gear.Ownership.OwnerId = _creature.Id;
        gear.Ownership.OwnerType = OwnerType.Creature;
        _context.Items.Add(gear);
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
        await using var fullRegenServiceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .AddSingleton<IOptionsSnapshot<CreatureRegenOptions>>(
                new TestOptionsSnapshot<CreatureRegenOptions>(fullHpRegenOptions)
            )
            .BuildServiceProvider();
        var handler =
            fullRegenServiceProvider.GetRequiredService<ApplyPassiveRegenCommandHandler>();

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
