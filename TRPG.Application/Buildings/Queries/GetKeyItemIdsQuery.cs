using Microsoft.EntityFrameworkCore;
using TRPG.Data;

namespace TRPG.Application.Buildings.Queries;

internal class GetKeyItemIdsQuery
{
    public required Guid RoomConnectorId { get; init; }
}

internal class GetKeyItemIdsQueryHandler(TrpgDbContext context)
{
    public async Task<IReadOnlyList<Guid>> Handle(
        GetKeyItemIdsQuery query,
        CancellationToken cancellationToken = default
    )
    {
        return await context
            .RoomConnectorKeys.Where(k => k.RoomConnectorId == query.RoomConnectorId)
            .Select(k => k.ItemId)
            .ToArrayAsync(cancellationToken);
    }
}
