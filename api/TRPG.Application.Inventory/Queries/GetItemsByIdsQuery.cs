using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Inventory.Queries;

public class GetItemsByIdsQuery
{
    public required IReadOnlyCollection<Guid> Ids { get; init; }
}

internal class GetItemsByIdsQueryHandler(IInventoryDbContext context)
    : IQueryHandler<GetItemsByIdsQuery, IReadOnlyDictionary<Guid, Item>>
{
    public async Task<IReadOnlyDictionary<Guid, Item>> Handle(
        GetItemsByIdsQuery query,
        CancellationToken cancellationToken = default
    )
    {
        return await context
            .Items.AsNoTracking()
            .Where(item => query.Ids.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, cancellationToken);
    }
}
