using TRPG.Application.Encounters.Events;
using TRPG.Encounters.Responses;

namespace TRPG.GameSessions.Hubs;

internal sealed class GuardEncounterStartedEventMapper
    : GameClientEventMapper<GuardEncounterStartedEvent>
{
    private static readonly string[] AllowedActions = ["PayFine", "GoToJail", "ResistArrest"];

    protected override IGameClientCall Map(GuardEncounterStartedEvent gameEvent) =>
        new GameClientCall<GuardEncounterState>(
            new GuardEncounterState(
                gameEvent.Encounter.Id,
                gameEvent.Encounter.GuardName,
                gameEvent.Encounter.LocationName!,
                gameEvent.Encounter.FineAmount,
                gameEvent.Encounter.JailHours,
                gameEvent.Encounter.RecentOffenses,
                AllowedActions
            ),
            static (client, arguments) => client.GuardEncounterStarted(arguments)
        );
}
