using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Commands;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Props.Commands;

public class VacateCreatureSeatCommand
{
    public required Guid CreatureId { get; init; }
}

internal class VacateCreatureSeatCommandHandler(IPropsDbContext context)
    : ICommandHandler<VacateCreatureSeatCommand>
{
    public async Task Handle(
        VacateCreatureSeatCommand command,
        CancellationToken cancellationToken = default
    )
    {
        await context
            .Props.OfType<Seat>()
            .Where(seat => seat.OccupantId == command.CreatureId)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(seat => seat.OccupantId, (Guid?)null),
                cancellationToken
            );
    }
}
