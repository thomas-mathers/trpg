using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Inventory.Queries;

public class GetGoldQuantitiesByOwnersQuery
{
    public required IReadOnlyCollection<Guid> OwnerIds { get; init; }
    public required OwnerType OwnerType { get; init; }
}

internal class GetGoldQuantitiesByOwnersQueryHandler(IInventoryDbContext context)
    : IQueryHandler<GetGoldQuantitiesByOwnersQuery, IReadOnlyDictionary<Guid, int>>
{
    public async Task<IReadOnlyDictionary<Guid, int>> Handle(
        GetGoldQuantitiesByOwnersQuery query,
        CancellationToken cancellationToken = default
    ) =>
        await context
            .Items.OfType<Gold>()
            .AsNoTracking()
            .Where(gold =>
                gold.Ownership.OwnerType == query.OwnerType
                && query.OwnerIds.AsEnumerable().Contains(gold.Ownership.OwnerId)
            )
            .ToDictionaryAsync(
                gold => gold.Ownership.OwnerId,
                gold => gold.Quantity,
                cancellationToken
            );
}
