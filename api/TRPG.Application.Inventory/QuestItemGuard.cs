using Microsoft.EntityFrameworkCore;
using TRPG.Data.ModuleContexts;

namespace TRPG.Application.Inventory;

internal class QuestItemGuard(IInventoryDbContext context)
{
    public async Task EnsureNotActiveQuestItems(
        IReadOnlyCollection<Guid> itemIds,
        CancellationToken cancellationToken
    )
    {
        var questItemId = await context
            .Items.Where(item => itemIds.Contains(item.Id) && item.IsQuestItem)
            .Select(item => item.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (questItemId != Guid.Empty)
        {
            throw new InvalidOperationException(
                $"Item {questItemId} is required for an active quest and cannot be removed from your inventory."
            );
        }
    }
}
