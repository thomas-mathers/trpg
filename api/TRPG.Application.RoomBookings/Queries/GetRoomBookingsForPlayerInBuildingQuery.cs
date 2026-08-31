using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data;
using TRPG.Domain.Models;

namespace TRPG.Application.RoomBookings.Queries;

public class GetRoomBookingsForPlayerInBuildingQuery
{
    public required Guid PlayerId { get; init; }
    public required Guid BuildingId { get; init; }
}

internal class GetRoomBookingsForPlayerInBuildingQueryHandler(TrpgDbContext context)
    : IQueryHandler<GetRoomBookingsForPlayerInBuildingQuery, IReadOnlyCollection<RoomBooking>>
{
    public async Task<IReadOnlyCollection<RoomBooking>> Handle(
        GetRoomBookingsForPlayerInBuildingQuery query,
        CancellationToken cancellationToken = default
    ) =>
        await context
            .RoomBookings.AsNoTracking()
            .Where(booking => booking.PlayerId == query.PlayerId)
            .Join(
                context.Rooms.AsNoTracking().Where(room => room.BuildingId == query.BuildingId),
                booking => booking.RoomId,
                room => room.Id,
                (booking, _) => booking
            )
            .ToArrayAsync(cancellationToken);
}
