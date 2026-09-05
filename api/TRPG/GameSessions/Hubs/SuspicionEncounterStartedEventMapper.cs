using TRPG.Application.Encounters.Events;
using TRPG.Encounters.Responses;

namespace TRPG.GameSessions.Hubs;

internal sealed class SuspicionEncounterStartedEventMapper
    : GameClientEventMapper<SuspicionEncounterStartedEvent>
{
    private static readonly string[] AllowedActions = ["Comply", "Flee"];

    protected override IGameClientCall Map(SuspicionEncounterStartedEvent gameEvent) =>
        new GameClientCall<SuspicionEncounterState>(
            new SuspicionEncounterState(
                gameEvent.Encounter.Id,
                gameEvent.Encounter.GuardName,
                gameEvent.Encounter.LocationName!,
                (SuspicionCause)gameEvent.Encounter.Cause,
                AllowedActions
            ),
            static (client, arguments) => client.SuspicionEncounterStarted(arguments)
        );
}
