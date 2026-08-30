using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data;
using TRPG.Domain.Models;

namespace TRPG.Application.Inventory.Queries;

public class GetKeyItemIdsByOwnerQuery
{
    public required ItemOwnerReference Owner { get; init; }
}

internal class GetKeyItemIdsByOwnerQueryHandler(TrpgDbContext context)
    : IQueryHandler<GetKeyItemIdsByOwnerQuery, IReadOnlySet<Guid>>
{
    public async Task<IReadOnlySet<Guid>> Handle(
        GetKeyItemIdsByOwnerQuery query,
        CancellationToken cancellationToken = default
    )
    {
        return await context
            .Items.AsNoTracking()
            .OfType<Key>()
            .Where(key =>
                key.Ownership.OwnerType == query.Owner.Type
                && key.Ownership.OwnerId == query.Owner.Id
                && key.Quantity > 0
            )
            .Select(key => key.Id)
            .ToHashSetAsync(cancellationToken);
    }
}
