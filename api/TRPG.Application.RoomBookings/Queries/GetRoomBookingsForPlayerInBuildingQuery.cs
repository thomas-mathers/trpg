using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Application.Worlds.Queries;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.RoomBookings.Queries;

public class GetRoomBookingsForPlayerInBuildingQuery
{
    public required Guid PlayerId { get; init; }
    public required Guid BuildingId { get; init; }
}

internal class GetRoomBookingsForPlayerInBuildingQueryHandler(
    IRoomBookingsDbContext context,
    IQueryHandler<GetRoomsByBuildingIdQuery, IReadOnlyCollection<Room>> getRoomsByBuildingId
) : IQueryHandler<GetRoomBookingsForPlayerInBuildingQuery, IReadOnlyCollection<RoomBooking>>
{
    public async Task<IReadOnlyCollection<RoomBooking>> Handle(
        GetRoomBookingsForPlayerInBuildingQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var rooms = await getRoomsByBuildingId.Handle(
            new GetRoomsByBuildingIdQuery { BuildingId = query.BuildingId },
            cancellationToken
        );
        var roomIds = rooms.Select(room => room.Id).ToArray();

        return await context
            .RoomBookings.AsNoTracking()
            .Where(booking =>
                booking.PlayerId == query.PlayerId
                && roomIds.AsEnumerable().Contains(booking.RoomId)
            )
            .ToArrayAsync(cancellationToken);
    }
}
