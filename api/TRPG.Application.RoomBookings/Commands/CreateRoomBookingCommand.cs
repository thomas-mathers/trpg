using TRPG.Application.Common.Commands;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.RoomBookings.Commands;

public class CreateRoomBookingCommand
{
    public required RoomBooking RoomBooking { get; init; }
}

internal class CreateRoomBookingCommandHandler(IRoomBookingsDbContext context)
    : ICommandHandler<CreateRoomBookingCommand>
{
    public async Task Handle(
        CreateRoomBookingCommand command,
        CancellationToken cancellationToken = default
    )
    {
        context.RoomBookings.Add(command.RoomBooking);
        await context.SaveChangesAsync(cancellationToken);
    }
}
