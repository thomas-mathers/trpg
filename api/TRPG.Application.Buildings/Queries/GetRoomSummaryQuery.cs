using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using TRPG.Application.Common.Handling;
using TRPG.Data;
using TRPG.Data.Models;

namespace TRPG.Application.Buildings.Queries;

public class GetRoomSummaryQuery
{
    public required Guid RoomId { get; init; }
}

public record RoomSummary(
    string RoomName,
    string RoomDescription,
    int RoomFloorNumber,
    Guid BuildingId,
    string BuildingName,
    BuildingType BuildingType,
    string? OwnerName,
    string? FactionName,
    string? FactionDescription
);

internal class GetRoomSummaryQueryHandler(TrpgDbContext context, IMemoryCache cache)
    : IQueryHandler<GetRoomSummaryQuery, RoomSummary?>
{
    public async Task<RoomSummary?> Handle(
        GetRoomSummaryQuery query,
        CancellationToken cancellationToken = default
    )
    {
        return await cache.GetOrCreateAsync(
            $"roomSummary:{query.RoomId}",
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
                    select new RoomSummary(
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
