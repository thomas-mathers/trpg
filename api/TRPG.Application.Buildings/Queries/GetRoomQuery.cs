using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using TRPG.Application.Buildings.Results;
using TRPG.Application.Common.Handling;
using TRPG.Data;
using TRPG.Domain.Models;

namespace TRPG.Application.Buildings.Queries;

public class GetRoomQuery
{
    public required Guid RoomId { get; init; }
}

internal class GetRoomQueryHandler(TrpgDbContext context, IMemoryCache cache)
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
                await (
                    from r in context.Rooms.AsNoTracking()
                    where r.Id == query.RoomId
                    join b in context.Buildings.AsNoTracking() on r.BuildingId equals b.Id
                    join bo in context.BuildingOwners on b.Id equals bo.BuildingId into ownersGroup
                    from bo in ownersGroup.DefaultIfEmpty()
                    join owner in context.Creatures on bo.OwnerId equals owner.Id into ownerGroup
                    from owner in ownerGroup.DefaultIfEmpty()
                    join f in context.Factions on b.FactionId equals (Guid?)f.Id into factionGroup
                    from f in factionGroup.DefaultIfEmpty()
                    select new RoomResult(
                        r.Name,
                        r.Description,
                        r.FloorNumber,
                        b.Id,
                        b.Name,
                        b.BuildingType,
                        owner != null ? owner.Name : null,
                        f != null ? f.Name : null,
                        f != null ? f.Description : null
                    )
                ).FirstOrDefaultAsync(cancellationToken)
        );
    }
}
