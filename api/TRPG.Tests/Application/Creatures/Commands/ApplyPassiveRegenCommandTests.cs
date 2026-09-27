using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TRPG.Application.Configuration;
using TRPG.Application.Creatures.Commands;
using TRPG.Application.Inventory.Commands;
using TRPG.Data;
using TRPG.Domain;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Creatures.Commands;

public sealed class ApplyPassiveRegenCommandTests(DatabaseFixture db)
    : IAsyncLifetime,
        IClassFixture<DatabaseFixture>
{
    private static readonly CreatureRegenOptions RegenOptions = new()
    {
        HpRegenPercentPerTick = 0.2f,
        ApRegenPercentPerTick = 0.25f,
        MpRegenPercentPerTick = 0.25f,
    };

    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private GameInstant _gameTime;
    private ApplyPassiveRegenCommandHandler _handler = null!;
    private readonly Creature _creature = Builders.MakeCreature(
        currentHp: 0,
        currentAp: 0,
        currentMp: 0
    );

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();

        _context.Creatures.Add(_creature);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

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

    private void SetGameTime(GameInstant gameTime) => _gameTime = gameTime;

    [Fact]
    public async Task Handle_ReturnsDetachedCreatures_ReflectingRegeneratedValues()
    {
        // Arrange
        SetGameTime(GameClock.Epoch + TimeSpan.FromSeconds(5));

        // Act
        var result = await _handler.Handle(
            new ApplyPassiveRegenCommand { GameTime = _gameTime, CreatureIds = [_creature.Id] },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(7, result[_creature.Id].CurrentHp);
        Assert.Equal(EntityState.Detached, _context.Entry(result[_creature.Id]).State);
    }

    [Fact]
    public async Task Handle_RegeneratesTowardEffectiveMaximum_WhenGearIsEquipped()
    {
        // Arrange
        var baseMaximumHp = _creature.MaximumHp;
        var gear = Builders.MakeArmor(
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

        await _serviceProvider
            .GetRequiredService<EquipInventoryItemCommandHandler>()
            .Handle(
                new EquipInventoryItemCommand
                {
                    CreatureId = _creature.Id,
                    ItemId = gear.Id,
                    Slot = EquipmentSlot.Chest,
                },
                TestContext.Current.CancellationToken
            );

        SetGameTime(GameClock.Epoch + TimeSpan.FromHours(1));

        var fullHpRegenOptions = new CreatureRegenOptions { HpRegenPercentPerTick = 1.0f };
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
            new ApplyPassiveRegenCommand { GameTime = _gameTime, CreatureIds = [_creature.Id] },
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
