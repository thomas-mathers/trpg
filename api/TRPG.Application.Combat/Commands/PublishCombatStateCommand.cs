using TRPG.Application.Combat.Events;
using TRPG.Application.Combat.Mappers;
using TRPG.Application.Combat.Queries;
using TRPG.Application.Common.Events;
using TRPG.Application.Common.Handling;

namespace TRPG.Application.Combat.Commands;

public class PublishCombatStateCommand
{
    public required Guid PlayerId { get; init; }
}

internal class PublishCombatStateCommandHandler(
    IQueryHandler<GetActiveFightCombatantsQuery, IReadOnlyList<Combatant>> getCombatants,
    IGameClientEventSink gameEvents
) : ICommandHandler<PublishCombatStateCommand>
{
    public async Task Handle(
        PublishCombatStateCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var combatants = await getCombatants.Handle(
            new GetActiveFightCombatantsQuery { PlayerId = command.PlayerId },
            cancellationToken
        );
        if (combatants.Count > 0)
            gameEvents.Enqueue(new CombatStartedEvent(combatants.ToCombatantStates()));
    }
}
