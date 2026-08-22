using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Exceptions;
using TRPG.Data;
using TRPG.Domain.Models;

namespace TRPG.Application.Creatures.Commands;

public class ReviveCreatureAtLocationCommand
{
    public required Guid CreatureId { get; init; }
    public required Guid LocationId { get; init; }
}

internal class ReviveCreatureAtLocationCommandHandler(TrpgDbContext context)
    : ICommandHandler<ReviveCreatureAtLocationCommand>
{
    public async Task Handle(
        ReviveCreatureAtLocationCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var creature =
            await context.Creatures.FindAsync([command.CreatureId], cancellationToken)
            ?? throw new EntityNotFoundException(nameof(Creature), command.CreatureId);

        creature.CurrentHp = creature.MaximumHp;
        creature.CurrentAp = creature.MaximumAp;
        creature.CurrentMp = creature.MaximumMp;
        creature.LocationId = command.LocationId;
        creature.State = CreatureState.Idle;

        await context.SaveChangesAsync(cancellationToken);
    }
}
