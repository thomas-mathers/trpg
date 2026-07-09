using Microsoft.EntityFrameworkCore;
using TRPG.Data;
using TRPG.Data.Models;

namespace TRPG.Services;

internal class InventoryService(TrpgDbContext context)
{
    public async Task Add(
        Guid creatureId,
        Guid itemId,
        int quantity,
        CancellationToken cancellationToken = default
    )
    {
        var item = await context.Items.FindAsync([itemId], cancellationToken);
        if (item is { IsStackable: true })
        {
            var existing = await context.InventoryItems.FirstOrDefaultAsync(
                i => i.CreatureId == creatureId && i.ItemId == itemId,
                cancellationToken
            );

            if (existing != null)
            {
                existing.Quantity += quantity;
                await context.SaveChangesAsync(cancellationToken);
                return;
            }
        }

        var maxIndex =
            await context
                .InventoryItems.Where(i => i.CreatureId == creatureId)
                .MaxAsync(i => (int?)i.Index, cancellationToken)
            ?? -1;

        var worldId = await context
            .Creatures.Where(p => p.Id == creatureId)
            .Select(p => p.WorldId)
            .FirstAsync(cancellationToken);

        context.InventoryItems.Add(
            new InventoryItem
            {
                Id = Guid.NewGuid(),
                CreatureId = creatureId,
                ItemId = itemId,
                Quantity = quantity,
                Index = maxIndex + 1,
                WorldId = worldId,
            }
        );

        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task Equip(
        Guid creatureId,
        Guid itemId,
        EquipmentSlot slot,
        CancellationToken cancellationToken = default
    )
    {
        var toEquip = await context.InventoryItems.FirstOrDefaultAsync(
            i => i.CreatureId == creatureId && i.ItemId == itemId,
            cancellationToken
        );

        if (toEquip == null)
        {
            throw new InvalidOperationException(
                $"Item {itemId} not found in creature {creatureId}'s inventory."
            );
        }

        var currentlyEquipped = await context.InventoryItems.FirstOrDefaultAsync(
            i => i.CreatureId == creatureId && i.EquippedSlot == slot,
            cancellationToken
        );

        if (currentlyEquipped != null)
        {
            currentlyEquipped.EquippedSlot = null;
        }

        toEquip.EquippedSlot = slot;

        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task Unequip(
        Guid creatureId,
        EquipmentSlot slot,
        CancellationToken cancellationToken = default
    )
    {
        var item = await context.InventoryItems.FirstOrDefaultAsync(
            i => i.CreatureId == creatureId && i.EquippedSlot == slot,
            cancellationToken
        );

        if (item == null)
        {
            throw new InvalidOperationException(
                $"No item equipped in slot {slot} for creature {creatureId}."
            );
        }

        item.EquippedSlot = null;

        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<InventoryItem>> GetAllByCreatureId(
        Guid creatureId,
        CancellationToken cancellationToken = default
    )
    {
        var list = await context
            .InventoryItems.AsNoTracking()
            .Include(i => i.Item)
            .Where(i => i.CreatureId == creatureId)
            .OrderBy(i => i.Index)
            .ToArrayAsync(cancellationToken);
        return list;
    }

    public async Task Remove(
        Guid creatureId,
        Guid itemId,
        int quantity,
        CancellationToken cancellationToken = default
    )
    {
        var existing = await context.InventoryItems.FirstOrDefaultAsync(
            i => i.CreatureId == creatureId && i.ItemId == itemId,
            cancellationToken
        );

        if (existing == null)
        {
            throw new InvalidOperationException(
                $"Item {itemId} not found in creature {creatureId}'s inventory."
            );
        }

        existing.Quantity -= quantity;

        if (existing.Quantity <= 0)
        {
            context.InventoryItems.Remove(existing);
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}
