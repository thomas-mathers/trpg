using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Combat.Queries;
using TRPG.Application.Inventory.Commands;
using TRPG.Data;
using TRPG.Data.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Combat.Queries;

[Collection("Database")]
public sealed class GetCreatureBasicAttackDamageQueryTests(DatabaseFixture db) : IAsyncLifetime
{
    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private GetCreatureBasicAttackDamageQueryHandler _handler = null!;
    private EquipInventoryItemCommandHandler _equipHandler = null!;
    private readonly Creature _creature = Builders.MakeCreature();

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<GetCreatureBasicAttackDamageQueryHandler>();
        _equipHandler = _serviceProvider.GetRequiredService<EquipInventoryItemCommandHandler>();

        _context.Creatures.Add(_creature);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_ReturnsPositiveValue_ForUnarmedCreature()
    {
        // Act
        var damagePerTurn = await _handler.Handle(
            new GetCreatureBasicAttackDamageQuery { CreatureId = _creature.Id },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.True(damagePerTurn > 0);
    }

    [Fact]
    public async Task Handle_ReturnsHigherValue_WhenStrongerWeaponIsEquipped()
    {
        // Arrange
        var baselineDamage = await _handler.Handle(
            new GetCreatureBasicAttackDamageQuery { CreatureId = _creature.Id },
            TestContext.Current.CancellationToken
        );
        var weapon = Builders.MakeWeaponItem(
            worldId: _creature.WorldId,
            minDamage: 500,
            maxDamage: 500
        );
        weapon.Quantity = 1;
        weapon.Ownership.OwnerId = _creature.Id;
        weapon.Ownership.OwnerType = OwnerType.Creature;
        _context.Items.Add(weapon);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        await _equipHandler.Handle(
            new EquipInventoryItemCommand
            {
                CreatureId = _creature.Id,
                ItemId = weapon.Id,
                Slot = EquipmentSlot.RightHand,
            },
            TestContext.Current.CancellationToken
        );

        // Act
        var damagePerTurn = await _handler.Handle(
            new GetCreatureBasicAttackDamageQuery { CreatureId = _creature.Id },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.True(damagePerTurn > baselineDamage);
    }
}
