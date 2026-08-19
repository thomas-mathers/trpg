using TRPG.Application.Encounters.Events;
using TRPG.Encounters.Responses;

namespace TRPG.GameSessions.Hubs;

internal sealed class GuardEncounterStartedEventMapper
    : GameClientEventMapper<GuardEncounterStartedEvent>
{
    protected override IGameClientCall Map(GuardEncounterStartedEvent gameEvent) =>
        new GameClientCall<GuardEncounterState>(
            new GuardEncounterState(
                gameEvent.State.EncounterId,
                gameEvent.State.GuardName,
                gameEvent.State.LocationName,
                gameEvent.State.FineAmount,
                gameEvent.State.JailHours,
                gameEvent.State.RecentOffenses
            ),
            static (client, arguments) => client.GuardEncounterStarted(arguments)
        );
}
