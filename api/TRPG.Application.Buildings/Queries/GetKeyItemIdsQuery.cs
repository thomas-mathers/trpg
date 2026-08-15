using Microsoft.EntityFrameworkCore;
using TRPG.Data;

namespace TRPG.Application.Buildings.Queries;

public class GetKeyItemIdsQuery
{
    public required Guid DoorConnectorId { get; init; }
}

public class GetKeyItemIdsQueryHandler(TrpgDbContext context)
{
    public async Task<IReadOnlyList<Guid>> Handle(
        GetKeyItemIdsQuery query,
        CancellationToken cancellationToken = default
    )
    {
        return await context
            .DoorConnectorKeys.Where(k => k.DoorConnectorId == query.DoorConnectorId)
            .Select(k => k.ItemId)
            .ToArrayAsync(cancellationToken);
    }
}
