using Microsoft.EntityFrameworkCore;
using TRPG.Data;
using TRPG.Data.Models;

namespace TRPG.Application.Inventory;

internal class GoldService(TrpgDbContext context)
{
    public async Task Transfer(
        Creature from,
        Creature to,
        CancellationToken cancellationToken = default
    )
    {
        var fromGoldItem = await FindGoldItem(from.Id, cancellationToken);
        // Fall back to the cached value when a creature has never gone through a path that
        // materializes its Gold item yet (e.g. a freshly-seeded creature) — the cache is
        // authoritative until an item exists to back it.
        var amount = fromGoldItem?.Quantity ?? from.Gold;
        if (amount <= 0)
        {
            return;
        }

        if (fromGoldItem != null)
        {
            fromGoldItem.Quantity = 0;
        }
        from.Gold = 0;

        var toGoldItem = await FindOrCreateGoldItem(to, cancellationToken);
        toGoldItem.Quantity += amount;
        to.Gold = toGoldItem.Quantity;
    }

    private async Task<Gold?> FindGoldItem(Guid creatureId, CancellationToken cancellationToken) =>
        await context
            .Items.OfType<Gold>()
            .FirstOrDefaultAsync(
                i =>
                    i.Ownership.OwnerType == OwnerType.Creature
                    && i.Ownership.OwnerId == creatureId,
                cancellationToken
            );

    private async Task<Gold> FindOrCreateGoldItem(
        Creature creature,
        CancellationToken cancellationToken
    )
    {
        var existing = await FindGoldItem(creature.Id, cancellationToken);
        if (existing != null)
        {
            return existing;
        }

        var goldItem = new Gold
        {
            WorldId = creature.WorldId,
            Name = "Gold",
            Ownership = new ItemOwnership { OwnerId = creature.Id, OwnerType = OwnerType.Creature },
        };
        context.Items.Add(goldItem);
        return goldItem;
    }
}
