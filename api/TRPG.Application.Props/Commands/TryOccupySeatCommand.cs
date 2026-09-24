using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Commands;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Props.Commands;

public class TryOccupySeatCommand
{
    public required Guid SeatId { get; init; }
    public required Guid CreatureId { get; init; }
}

internal class TryOccupySeatCommandHandler(IPropsDbContext context)
    : ICommandHandler<TryOccupySeatCommand, bool>
{
    public async Task<bool> Handle(
        TryOccupySeatCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var updated = await context
            .Props.OfType<Seat>()
            .Where(seat => seat.Id == command.SeatId && seat.OccupantId == null)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(seat => seat.OccupantId, command.CreatureId),
                cancellationToken
            );

        return updated == 1;
    }
}
