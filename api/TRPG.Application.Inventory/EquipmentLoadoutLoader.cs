using Microsoft.EntityFrameworkCore;
using TRPG.Data;
using TRPG.Domain.Models;

namespace TRPG.Application.Inventory;

internal class EquipmentLoadoutLoader(TrpgDbContext context)
{
    public async Task<IReadOnlyList<Item>> LoadOwnedItems(
        Guid creatureId,
        CancellationToken cancellationToken
    )
    {
        var creatureExists = await context.Creatures.AnyAsync(
            c => c.Id == creatureId,
            cancellationToken
        );
        if (!creatureExists)
        {
            throw new InvalidOperationException($"Creature {creatureId} not found.");
        }

        return await context
            .Items.Where(i =>
                i.Ownership.OwnerType == OwnerType.Creature && i.Ownership.OwnerId == creatureId
            )
            .ToArrayAsync(cancellationToken);
    }
}
