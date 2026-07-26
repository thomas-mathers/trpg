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
    public async Task<IReadOnlyList<Item>> Handle(
        GetInventoryByCreatureIdQuery query,
        CancellationToken cancellationToken = default
    )
    {
        return await context
            .Items.AsNoTracking()
            .Where(i =>
                i.Ownership.OwnerType == OwnerType.Creature
                && i.Ownership.OwnerId == query.CreatureId
                && !(i is Gold)
            )
            .OrderBy(i => i.Ownership.SortOrder)
            .ToArrayAsync(cancellationToken);
    }
}
