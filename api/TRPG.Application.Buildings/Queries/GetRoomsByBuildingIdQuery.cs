using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data;
using TRPG.Domain.Models;

namespace TRPG.Application.Buildings.Queries;

public class GetRoomsByBuildingIdQuery
{
    public required Guid BuildingId { get; init; }
}

internal class GetRoomsByBuildingIdQueryHandler(TrpgDbContext context)
    : IQueryHandler<GetRoomsByBuildingIdQuery, IReadOnlyCollection<Room>>
{
    public async Task<IReadOnlyCollection<Room>> Handle(
        GetRoomsByBuildingIdQuery query,
        CancellationToken cancellationToken = default
    ) =>
        await context
            .Rooms.AsNoTracking()
            .Where(room => room.BuildingId == query.BuildingId)
            .ToArrayAsync(cancellationToken);
}
