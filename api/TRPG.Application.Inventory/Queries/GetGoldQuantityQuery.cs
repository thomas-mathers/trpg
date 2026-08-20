using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data;
using TRPG.Domain.Models;

namespace TRPG.Application.Inventory.Queries;

public class GetGoldQuantityQuery
{
    public required ItemOwnerReference Owner { get; init; }
}

internal class GetGoldQuantityQueryHandler(TrpgDbContext context)
    : IQueryHandler<GetGoldQuantityQuery, int>
{
    public async Task<int> Handle(
        GetGoldQuantityQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var gold = await context
            .Items.OfType<Gold>()
            .AsNoTracking()
            .FirstOrDefaultAsync(
                item =>
                    item.Ownership.OwnerId == query.Owner.Id
                    && item.Ownership.OwnerType == query.Owner.Type,
                cancellationToken
            );

        return gold?.Quantity ?? 0;
    }
}
