using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Inventory.Queries;

public class GetItemCountsByOwnersQuery
{
    public required IReadOnlyCollection<Guid> OwnerIds { get; init; }
    public required OwnerType OwnerType { get; init; }
}

internal class GetItemCountsByOwnersQueryHandler(IInventoryDbContext context)
    : IQueryHandler<GetItemCountsByOwnersQuery, IReadOnlyDictionary<Guid, int>>
{
    public async Task<IReadOnlyDictionary<Guid, int>> Handle(
        GetItemCountsByOwnersQuery query,
        CancellationToken cancellationToken = default
    ) =>
        await context
            .Items.AsNoTracking()
            .Where(item =>
                item.Ownership.OwnerType == query.OwnerType
                && query.OwnerIds.AsEnumerable().Contains(item.Ownership.OwnerId)
                && item.Quantity > 0
            )
            .GroupBy(item => item.Ownership.OwnerId)
            .Select(group => new { OwnerId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(group => group.OwnerId, group => group.Count, cancellationToken);
}
