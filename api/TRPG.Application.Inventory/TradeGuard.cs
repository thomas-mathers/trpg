using Microsoft.EntityFrameworkCore;
using TRPG.Data.ModuleContexts;

namespace TRPG.Application.Inventory;

internal class TradeGuard(IInventoryDbContext context)
{
    public async Task EnsureAllTradeable(
        IReadOnlyCollection<Guid> itemIds,
        CancellationToken cancellationToken
    )
    {
        var untradeableItemId = await context
            .Items.Where(item => itemIds.Contains(item.Id) && !item.CanTrade)
            .Select(item => item.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (untradeableItemId != Guid.Empty)
        {
            throw new InvalidOperationException(
                $"Item {untradeableItemId} cannot be traded away right now."
            );
        }
    }
}
