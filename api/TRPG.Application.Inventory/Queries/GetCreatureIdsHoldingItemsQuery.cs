using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Inventory.Queries;

public class GetCreatureIdsHoldingItemsQuery
{
    public required IReadOnlyCollection<Guid> ItemIds { get; init; }
}

internal class GetCreatureIdsHoldingItemsQueryHandler(IInventoryDbContext context)
    : IQueryHandler<GetCreatureIdsHoldingItemsQuery, IReadOnlyCollection<Guid>>
{
    public async Task<IReadOnlyCollection<Guid>> Handle(
        GetCreatureIdsHoldingItemsQuery query,
        CancellationToken cancellationToken = default
    ) =>
        await context
            .Items.AsNoTracking()
            .Where(item =>
                query.ItemIds.AsEnumerable().Contains(item.Id)
                && item.Ownership.OwnerType == OwnerType.Creature
            )
            .Select(item => item.Ownership.OwnerId)
            .Distinct()
            .ToArrayAsync(cancellationToken);
}
