using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Queries;
using TRPG.Application.Props.Commands;
using TRPG.Application.Props.Queries;
using TRPG.Application.Worlds.Queries;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.RoomBookings.Commands;

public class DeleteRoomBookingsCommand
{
    public required IReadOnlyCollection<Guid> RoomBookingIds { get; init; }
}

internal class DeleteRoomBookingsCommandHandler(
    IRoomBookingsDbContext context,
    IQueryHandler<GetRoomsByIdsQuery, IReadOnlyDictionary<Guid, Room>> getRoomsByIds,
    IQueryHandler<GetBedsByLocationIdsQuery, IReadOnlyDictionary<Guid, Bed>> getBedsByLocationIds,
    ICommandHandler<SetBedAssignmentCommand> setBedAssignment
) : ICommandHandler<DeleteRoomBookingsCommand>
{
    public async Task Handle(
        DeleteRoomBookingsCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var roomIds = await context
            .RoomBookings.AsNoTracking()
            .Where(booking => command.RoomBookingIds.AsEnumerable().Contains(booking.Id))
            .Select(booking => booking.RoomId)
            .ToArrayAsync(cancellationToken);

        if (roomIds.Length > 0)
        {
            await ClearBedAssignments(roomIds, cancellationToken);
        }

        await context
            .RoomBookings.Where(booking =>
                command.RoomBookingIds.AsEnumerable().Contains(booking.Id)
            )
            .ExecuteDeleteAsync(cancellationToken);
    }

    private async Task ClearBedAssignments(
        IReadOnlyCollection<Guid> roomIds,
        CancellationToken cancellationToken
    )
    {
        var rooms = await getRoomsByIds.Handle(
            new GetRoomsByIdsQuery { Ids = roomIds },
            cancellationToken
        );

        var beds = await getBedsByLocationIds.Handle(
            new GetBedsByLocationIdsQuery
            {
                LocationIds = rooms.Values.Select(room => room.LocationId).ToArray(),
            },
            cancellationToken
        );

        await setBedAssignment.Handle(
            new SetBedAssignmentCommand
            {
                BedIds = beds.Values.Select(bed => bed.Id).ToArray(),
                AssignedCreatureId = null,
            },
            cancellationToken
        );
    }
}
