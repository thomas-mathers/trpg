using Microsoft.EntityFrameworkCore;
using TRPG.Data;
using TRPG.Data.Models;

namespace TRPG.Application.Buildings.Queries;

internal class GetConnectorsByRoomIdQuery
{
    public required Guid RoomId { get; init; }
}

internal class GetConnectorsByRoomIdQueryHandler(TrpgDbContext context)
{
    public async Task<IReadOnlyCollection<RoomConnector>> Handle(
        GetConnectorsByRoomIdQuery query,
        CancellationToken cancellationToken = default
    )
    {
        return await context
            .Props.AsNoTracking()
            .Where(p => p.RoomId == query.RoomId)
            .OfType<RoomConnector>()
            .ToArrayAsync(cancellationToken);
    }
}
