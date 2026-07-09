using Microsoft.EntityFrameworkCore;
using TRPG.Data;
using TRPG.Data.Models;

namespace TRPG.Application.Buildings.Queries;

internal class GetFrontDoorQuery
{
    public required Guid RoomId { get; init; }
}

internal class GetFrontDoorQueryHandler(TrpgDbContext context)
{
    public async Task<RoomConnector?> Handle(
        GetFrontDoorQuery query,
        CancellationToken cancellationToken = default
    )
    {
        return await context
            .Props.AsNoTracking()
            .Where(p => p.RoomId == query.RoomId)
            .OfType<RoomConnector>()
            .FirstOrDefaultAsync(c => c.DestinationRoomId == null, cancellationToken);
    }
}
