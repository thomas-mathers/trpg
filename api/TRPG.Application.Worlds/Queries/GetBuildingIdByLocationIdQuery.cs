using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using TRPG.Application.Common.Queries;
using TRPG.Data.ModuleContexts;

namespace TRPG.Application.Worlds.Queries;

public class GetBuildingIdByLocationIdQuery
{
    public required Guid LocationId { get; init; }
}

// A location's owning building never changes at runtime, and every kill in a single combat round
// shares one LocationId — caching avoids re-querying the same answer once per kill.
internal class GetBuildingIdByLocationIdQueryHandler(IWorldsDbContext context, IMemoryCache cache)
    : IQueryHandler<GetBuildingIdByLocationIdQuery, Guid?>
{
    public Task<Guid?> Handle(
        GetBuildingIdByLocationIdQuery query,
        CancellationToken cancellationToken = default
    ) =>
        cache.GetOrCreateAsync(
            $"buildingIdByLocationId:{query.LocationId}",
            async _ =>
                await (
                    from location in context.Locations.AsNoTracking()
                    where location.Id == query.LocationId
                    join room in context.Rooms.AsNoTracking() on location.RoomId equals room.Id
                    select (Guid?)room.BuildingId
                ).FirstOrDefaultAsync(cancellationToken)
        );
}
