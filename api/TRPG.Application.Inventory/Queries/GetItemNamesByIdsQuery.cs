using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data.ModuleContexts;

namespace TRPG.Application.Inventory.Queries;

public class GetItemNamesByIdsQuery
{
    public required Guid WorldId { get; init; }
    public required IReadOnlyCollection<Guid> ItemIds { get; init; }
}

internal class GetItemNamesByIdsQueryHandler(IInventoryDbContext context)
    : IQueryHandler<GetItemNamesByIdsQuery, IReadOnlyDictionary<Guid, string>>
{
    public async Task<IReadOnlyDictionary<Guid, string>> Handle(
        GetItemNamesByIdsQuery query,
        CancellationToken cancellationToken = default
    ) =>
        await context
            .Items.AsNoTracking()
            .Where(item =>
                item.WorldId == query.WorldId && query.ItemIds.AsEnumerable().Contains(item.Id)
            )
            .ToDictionaryAsync(item => item.Id, item => item.Name, cancellationToken);
}
