using Microsoft.EntityFrameworkCore;
using TRPG.Data;
using TRPG.Domain.Models;

namespace TRPG.Application.Inventory;

internal class GoldLoader(TrpgDbContext context)
{
    public Task<Gold?> FindGold(ItemOwnerReference owner, CancellationToken cancellationToken) =>
        context
            .Items.OfType<Gold>()
            .FirstOrDefaultAsync(
                item =>
                    item.Ownership.OwnerId == owner.Id && item.Ownership.OwnerType == owner.Type,
                cancellationToken
            );
}
