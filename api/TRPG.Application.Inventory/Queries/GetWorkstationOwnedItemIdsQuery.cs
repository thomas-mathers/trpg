using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data;
using TRPG.Domain.Models;

namespace TRPG.Application.Inventory.Queries;

public class GetWorkstationOwnedItemIdsQuery
{
    public required IReadOnlyCollection<Guid> ItemIds { get; init; }
}

internal class GetWorkstationOwnedItemIdsQueryHandler(TrpgDbContext context)
    : IQueryHandler<GetWorkstationOwnedItemIdsQuery, IReadOnlyDictionary<Guid, Guid>>
{
    public async Task<IReadOnlyDictionary<Guid, Guid>> Handle(
        GetWorkstationOwnedItemIdsQuery query,
        CancellationToken cancellationToken = default
    ) =>
        await context
            .Items.AsNoTracking()
            .Where(item =>
                query.ItemIds.AsEnumerable().Contains(item.Id)
                && item.Ownership.OwnerType == OwnerType.Workstation
            )
            .ToDictionaryAsync(item => item.Id, item => item.Ownership.OwnerId, cancellationToken);
}
