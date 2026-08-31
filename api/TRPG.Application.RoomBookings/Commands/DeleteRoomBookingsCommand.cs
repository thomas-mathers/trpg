using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Commands;
using TRPG.Data;

namespace TRPG.Application.RoomBookings.Commands;

public class DeleteRoomBookingsCommand
{
    public required IReadOnlyCollection<Guid> RoomBookingIds { get; init; }
}

internal class DeleteRoomBookingsCommandHandler(TrpgDbContext context)
    : ICommandHandler<DeleteRoomBookingsCommand>
{
    public async Task Handle(
        DeleteRoomBookingsCommand command,
        CancellationToken cancellationToken = default
    ) =>
        await context
            .RoomBookings.Where(booking =>
                command.RoomBookingIds.AsEnumerable().Contains(booking.Id)
            )
            .ExecuteDeleteAsync(cancellationToken);
}
