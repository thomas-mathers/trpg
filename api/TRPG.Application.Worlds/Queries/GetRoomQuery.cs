using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using TRPG.Application.Common.Queries;
using TRPG.Application.Factions.Queries;
using TRPG.Application.Worlds.Results;
using TRPG.Data;
using TRPG.Domain.Models;

namespace TRPG.Application.Worlds.Queries;

public class GetRoomQuery
{
    public required Guid RoomId { get; init; }
}

internal class GetRoomQueryHandler(
    TrpgDbContext context,
    IMemoryCache cache,
    IQueryHandler<GetFactionsByIdsQuery, IReadOnlyDictionary<Guid, Faction>> getFactionsByIds
) : IQueryHandler<GetRoomQuery, RoomResult?>
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
                    join owner in context.Creatures on bo.OwnerId equals owner.Id into ownerGroup
                    from owner in ownerGroup.DefaultIfEmpty()
                    select new
                    {
                        r.Name,
                        r.Description,
                        r.FloorNumber,
                        BuildingId = b.Id,
                        BuildingName = b.Name,
                        b.BuildingType,
                        OwnerName = owner != null ? owner.Name : null,
                        b.FactionId,
                    }
                ).FirstOrDefaultAsync(cancellationToken);

                if (raw == null)
                {
                    return null;
                }

                Faction? faction = null;
                if (raw.FactionId is { } factionId)
                {
                    var factionsById = await getFactionsByIds.Handle(
                        new GetFactionsByIdsQuery { Ids = [factionId] },
                        cancellationToken
                    );
                    factionsById.TryGetValue(factionId, out faction);
                }

                return new RoomResult(
                    raw.Name,
                    raw.Description,
                    raw.FloorNumber,
                    raw.BuildingId,
                    raw.BuildingName,
                    raw.BuildingType,
                    raw.OwnerName,
                    faction?.Name,
                    faction?.Description
                );
            }
        );
    }
}
