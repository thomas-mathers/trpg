using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.Inventory.Commands;
using TRPG.Application.Inventory.Queries;
using TRPG.Data;
using TRPG.Data.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Inventory.Queries;

[Collection("Database")]
public sealed class PreviewEquipItemBasicAttackDamageQueryTests(DatabaseFixture db) : IAsyncLifetime
{
    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private PreviewEquipItemBasicAttackDamageQueryHandler _previewHandler = null!;
    private GetCreatureBasicAttackDamageQueryHandler _getDamageHandler = null!;
    private EquipInventoryItemCommandHandler _equipHandler = null!;
    private readonly Creature _creature = Builders.MakeCreature();
    private readonly Weapon _weapon = Builders.MakeWeaponItem();

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _previewHandler =
            _serviceProvider.GetRequiredService<PreviewEquipItemBasicAttackDamageQueryHandler>();
        _getDamageHandler =
            _serviceProvider.GetRequiredService<GetCreatureBasicAttackDamageQueryHandler>();
        _equipHandler = _serviceProvider.GetRequiredService<EquipInventoryItemCommandHandler>();

        GiveToCreature(_weapon);

        _context.Creatures.Add(_creature);
        _context.Items.Add(_weapon);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    private void GiveToCreature(Item item)
    {
        item.Quantity = 1;
        item.Ownership.OwnerId = _creature.Id;
        item.Ownership.OwnerType = OwnerType.Creature;
    }

    [Fact]
    public async Task Handle_MatchesTheValueAfterActuallyEquipping()
    {
        // Act
        var previewedDamage = await _previewHandler.Handle(
            new PreviewEquipItemBasicAttackDamageQuery
            {
                CreatureId = _creature.Id,
                ItemId = _weapon.Id,
                Slot = EquipmentSlot.RightHand,
            },
            TestContext.Current.CancellationToken
        );
        await _equipHandler.Handle(
            new EquipInventoryItemCommand
            {
                CreatureId = _creature.Id,
                ItemId = _weapon.Id,
                Slot = EquipmentSlot.RightHand,
            },
            TestContext.Current.CancellationToken
        );
        var actualDamage = await _getDamageHandler.Handle(
            new GetCreatureBasicAttackDamageQuery { CreatureId = _creature.Id },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(actualDamage, previewedDamage, precision: 3);
    }

    [Fact]
    public async Task Handle_DoesNotPersistAnyChanges()
    {
        // Act
        await _previewHandler.Handle(
            new PreviewEquipItemBasicAttackDamageQuery
            {
                CreatureId = _creature.Id,
                ItemId = _weapon.Id,
                Slot = EquipmentSlot.RightHand,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        var updatedWeapon = await verifyContext.Items.FindAsync(
            [_weapon.Id],
            TestContext.Current.CancellationToken
        );
        Assert.Null(updatedWeapon!.Ownership.EquippedSlot);
    }
}
