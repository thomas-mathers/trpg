using Microsoft.EntityFrameworkCore;
using TRPG.Data;
using TRPG.Data.Models;

namespace TRPG.Application.Inventory.Queries;

internal class GetInventoryByCreatureIdQuery
{
    public required Guid CreatureId { get; init; }
}

internal class GetInventoryByCreatureIdQueryHandler(TrpgDbContext context)
{
    public async Task<IReadOnlyCollection<InventoryItem>> Handle(
        GetInventoryByCreatureIdQuery query,
        CancellationToken cancellationToken = default
    )
    {
        return await context
            .InventoryItems.AsNoTracking()
            .Include(i => i.Item)
            .Where(i => i.CreatureId == query.CreatureId)
            .OrderBy(i => i.Index)
            .ToArrayAsync(cancellationToken);
    }
}
