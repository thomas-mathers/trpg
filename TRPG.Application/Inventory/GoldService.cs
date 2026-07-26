using Microsoft.EntityFrameworkCore;
using TRPG.Data;
using TRPG.Data.Models;

namespace TRPG.Application.Inventory;

internal class GoldService(TrpgDbContext context)
{
    public async Task Transfer(
        Gold sourceGoldItem,
        Creature fromCreature,
        Creature toCreature,
        CancellationToken cancellationToken = default
    )
    {
        var amount = sourceGoldItem.Quantity;
        if (amount <= 0)
        {
            return;
        }

        sourceGoldItem.Quantity = 0;
        fromCreature.Gold = 0;

        var toGoldItem = await FindOrCreateGoldItem(toCreature, cancellationToken);
        toGoldItem.Quantity += amount;
        toCreature.Gold = toGoldItem.Quantity;
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
