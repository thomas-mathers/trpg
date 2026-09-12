using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Worlds.Queries;

public class GetRoomsByRoleQuery
{
    public required Guid WorldId { get; init; }
    public required Guid StateId { get; init; }
    public required RoomRole Role { get; init; }
}

internal class GetRoomsByRoleQueryHandler(IWorldsDbContext context)
    : IQueryHandler<GetRoomsByRoleQuery, IReadOnlyList<Room>>
{
    public async Task<IReadOnlyList<Room>> Handle(
        GetRoomsByRoleQuery query,
        CancellationToken cancellationToken = default
    ) =>
        await context
            .Rooms.AsNoTracking()
            .Where(room => room.WorldId == query.WorldId && room.Role == query.Role)
            .Join(
                context
                    .Locations.AsNoTracking()
                    .Where(location => location.StateId == query.StateId),
                room => room.LocationId,
                location => location.Id,
                (room, _) => room
            )
            .ToArrayAsync(cancellationToken);
}
