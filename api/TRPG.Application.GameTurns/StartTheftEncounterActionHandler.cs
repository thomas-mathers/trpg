using TRPG.Application.Common.Events;
using TRPG.Application.Common.Queries;
using TRPG.Application.Encounters.Events;
using TRPG.Application.Encounters.Queries;
using TRPG.Domain.Models;

namespace TRPG.Application.GameTurns;

internal class StartTheftEncounterActionHandler(
    GameActionRunner actionRunner,
    IQueryHandler<GetActiveEncounterQuery, Encounter?> getActiveEncounter,
    IGameClientEventSink gameEvents
)
{
    public Task<ActionOutcome> Handle(
        GameTurnSession session,
        Guid encounterId,
        CancellationToken cancellationToken = default
    ) => actionRunner.Run(session, ct => Resolve(session, encounterId, ct), cancellationToken);

    private async Task<ActionOutcome> Resolve(
        GameTurnSession session,
        Guid encounterId,
        CancellationToken cancellationToken
    )
    {
        var encounter = await getActiveEncounter.Handle(
            new GetActiveEncounterQuery { PlayerId = session.PlayerId },
            cancellationToken
        );

        if (
            encounter is not TheftEncounter theftEncounter
            || theftEncounter.Id != encounterId
            || theftEncounter.WorldId != session.WorldId
        )
        {
            return ActionOutcome.Failed(ActionFailure.NoEncounter);
        }

        gameEvents.Enqueue(new TheftEncounterStartedEvent(theftEncounter));

        return ActionOutcome.Success;
    }
}
