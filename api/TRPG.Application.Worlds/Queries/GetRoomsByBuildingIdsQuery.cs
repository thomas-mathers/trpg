using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Worlds.Queries;

public class GetRoomsByBuildingIdsQuery
{
    public required IReadOnlyCollection<Guid> BuildingIds { get; init; }
}

internal class GetRoomsByBuildingIdsQueryHandler(IWorldsDbContext context)
    : IQueryHandler<GetRoomsByBuildingIdsQuery, IReadOnlyCollection<Room>>
{
    public async Task<IReadOnlyCollection<Room>> Handle(
        GetRoomsByBuildingIdsQuery query,
        CancellationToken cancellationToken = default
    ) =>
        await context
            .Rooms.AsNoTracking()
            .Where(room => query.BuildingIds.AsEnumerable().Contains(room.BuildingId))
            .ToArrayAsync(cancellationToken);
}
