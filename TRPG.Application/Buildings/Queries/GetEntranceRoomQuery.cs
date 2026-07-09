using Microsoft.EntityFrameworkCore;
using TRPG.Data;
using TRPG.Data.Models;

namespace TRPG.Application.Buildings.Queries;

internal class GetEntranceRoomQuery
{
    public required Guid BuildingId { get; init; }
}

internal class GetEntranceRoomQueryHandler(TrpgDbContext context)
{
    public async Task<Room?> Handle(
        GetEntranceRoomQuery query,
        CancellationToken cancellationToken = default
    )
    {
        return await context
            .Rooms.AsNoTracking()
            .FirstOrDefaultAsync(
                r => r.BuildingId == query.BuildingId && r.FloorNumber == 0,
                cancellationToken
            );
    }
}
