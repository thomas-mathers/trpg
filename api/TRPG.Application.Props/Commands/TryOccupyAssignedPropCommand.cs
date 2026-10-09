using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Commands;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Props.Commands;

public class TryOccupyAssignedPropCommand
{
    public required Guid PropId { get; init; }
    public required Guid CreatureId { get; init; }
    public required CreatureJobAction Action { get; init; }
}

internal class TryOccupyAssignedPropCommandHandler(IPropsDbContext context)
    : ICommandHandler<TryOccupyAssignedPropCommand, bool>
{
    public async Task<bool> Handle(
        TryOccupyAssignedPropCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var updated = command.Action switch
        {
            CreatureJobAction.Sleep => await context
                .Props.OfType<Bed>()
                .Where(prop =>
                    prop.Id == command.PropId
                    && prop.AssignedCreatureId == command.CreatureId
                    && prop.OccupantId == null
                )
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(prop => prop.OccupantId, command.CreatureId),
                    cancellationToken
                ),
            CreatureJobAction.Work => await context
                .Props.OfType<Workstation>()
                .Where(prop =>
                    prop.Id == command.PropId
                    && prop.AssignedCreatureId == command.CreatureId
                    && prop.OccupantId == null
                )
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(prop => prop.OccupantId, command.CreatureId),
                    cancellationToken
                ),
            _ => 0,
        };
        return updated == 1;
    }
}
