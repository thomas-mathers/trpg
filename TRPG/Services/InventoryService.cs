using System.Collections.ObjectModel;
using Microsoft.EntityFrameworkCore;
using TRPG.Data;
using TRPG.Models;

namespace TRPG.Services;

internal class InventoryService(TrpgDbContext context) {
    public async Task Add(Guid personId, Guid itemId, int quantity, CancellationToken cancellationToken = default) {
        var item = await context.Items.FindAsync([itemId], cancellationToken);
        if (item is { IsStackable: true }) {
            var existing = await context.InventoryItems
                .FirstOrDefaultAsync(i => i.PersonId == personId && i.ItemId == itemId, cancellationToken);

            if (existing != null) {
                existing.Quantity += quantity;
                await context.SaveChangesAsync(cancellationToken);
                return;
            }
        }

        var maxIndex = await context.InventoryItems
            .Where(i => i.PersonId == personId)
            .MaxAsync(i => (int?) i.Index, cancellationToken) ?? -1;

        context.InventoryItems.Add(new InventoryItem {
            Id = Guid.NewGuid(),
            PersonId = personId,
            ItemId = itemId,
            Quantity = quantity,
            Index = maxIndex + 1
        });

        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task Equip(Guid personId, Guid itemId, EquipmentSlot slot,
        CancellationToken cancellationToken = default) {
        var toEquip = await context.InventoryItems
            .FirstOrDefaultAsync(i => i.PersonId == personId && i.ItemId == itemId, cancellationToken);

        if (toEquip == null) {
            throw new InvalidOperationException($"Item {itemId} not found in person {personId}'s inventory.");
        }

        var currentlyEquipped = await context.InventoryItems
            .FirstOrDefaultAsync(i => i.PersonId == personId && i.EquippedSlot == slot, cancellationToken);

        if (currentlyEquipped != null) {
            currentlyEquipped.EquippedSlot = null;
        }

        toEquip.EquippedSlot = slot;

        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task Unequip(Guid personId, EquipmentSlot slot, CancellationToken cancellationToken = default) {
        var item = await context.InventoryItems
            .FirstOrDefaultAsync(i => i.PersonId == personId && i.EquippedSlot == slot, cancellationToken);

        if (item == null) {
            throw new InvalidOperationException($"No item equipped in slot {slot} for person {personId}.");
        }

        item.EquippedSlot = null;

        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<ReadOnlyCollection<InventoryItem>> GetAllByPersonId(Guid personId,
        CancellationToken cancellationToken = default) {
        var list = await context.InventoryItems
            .Include(i => i.Item)
            .Where(i => i.PersonId == personId)
            .OrderBy(i => i.Index)
            .ToListAsync(cancellationToken);
        return list.AsReadOnly();
    }

    public async Task Remove(Guid personId, Guid itemId, int quantity, CancellationToken cancellationToken = default) {
        var existing = await context.InventoryItems
            .FirstOrDefaultAsync(i => i.PersonId == personId && i.ItemId == itemId, cancellationToken);

        if (existing == null) {
            throw new InvalidOperationException($"Item {itemId} not found in person {personId}'s inventory.");
        }

        existing.Quantity -= quantity;

        if (existing.Quantity <= 0) {
            context.InventoryItems.Remove(existing);
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}