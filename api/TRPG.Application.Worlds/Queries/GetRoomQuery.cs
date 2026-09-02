using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using TRPG.Application.Common.Queries;
using TRPG.Application.Worlds.Results;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Worlds.Queries;

public class GetRoomQuery
{
    public required Guid RoomId { get; init; }
}

internal class GetRoomQueryHandler(IWorldsDbContext context, IMemoryCache cache)
    : IQueryHandler<GetRoomQuery, RoomResult?>
{
    public async Task<RoomResult?> Handle(
        GetRoomQuery query,
        CancellationToken cancellationToken = default
    )
    {
        return await cache.GetOrCreateAsync(
            $"roomResult:{query.RoomId}",
            async _ =>
            {
                var raw = await (
                    from r in context.Rooms.AsNoTracking()
                    where r.Id == query.RoomId
                    join b in context.Buildings.AsNoTracking() on r.BuildingId equals b.Id
                    join bo in context.BuildingOwners on b.Id equals bo.BuildingId into ownersGroup
                    from bo in ownersGroup.DefaultIfEmpty()
                    select new
                    {
                        r.Name,
                        r.Description,
                        r.FloorNumber,
                        BuildingId = b.Id,
                        BuildingName = b.Name,
                        b.BuildingType,
                        OwnerId = bo != null ? (Guid?)bo.OwnerId : null,
                        b.FactionId,
                    }
                ).FirstOrDefaultAsync(cancellationToken);

                if (raw == null)
                {
                    return null;
                }

                return new RoomResult(
                    RoomName: raw.Name,
                    RoomDescription: raw.Description,
                    RoomFloorNumber: raw.FloorNumber,
                    BuildingId: raw.BuildingId,
                    BuildingName: raw.BuildingName,
                    BuildingType: raw.BuildingType,
                    OwnerId: raw.OwnerId,
                    FactionId: raw.FactionId
                );
            }
        );
    }
}
