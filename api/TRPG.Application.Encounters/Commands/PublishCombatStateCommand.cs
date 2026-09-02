using TRPG.Application.Combat.Results;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Events;
using TRPG.Application.Common.Queries;
using TRPG.Application.Encounters.Events;
using TRPG.Application.Encounters.Queries;
using TRPG.Domain.Models;

namespace TRPG.Application.Encounters.Commands;

public class PublishCombatStateCommand
{
    public required Guid PlayerId { get; init; }
}

internal class PublishCombatStateCommandHandler(
    IQueryHandler<GetActiveFightQuery, FightEncounter?> getActiveFight,
    IQueryHandler<GetActiveFightCombatantsQuery, IReadOnlyList<CombatantResult>> getCombatants,
    IGameClientEventSink gameEvents
) : ICommandHandler<PublishCombatStateCommand>
{
    public async Task Handle(
        PublishCombatStateCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var fight = await getActiveFight.Handle(
            new GetActiveFightQuery { PlayerId = command.PlayerId },
            cancellationToken
        );
        if (fight == null)
        {
            return;
        }

        var combatants = await getCombatants.Handle(
            new GetActiveFightCombatantsQuery { PlayerId = command.PlayerId },
            cancellationToken
        );
        if (combatants.Count > 0)
            gameEvents.Enqueue(new CombatStartedEvent(fight.Id, combatants));
    }
}
