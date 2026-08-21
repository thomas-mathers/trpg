using TRPG.Application.Encounters.Events;
using TRPG.Encounters.Responses;

namespace TRPG.GameSessions.Hubs;

internal sealed class TheftEncounterStartedEventMapper
    : GameClientEventMapper<TheftEncounterStartedEvent>
{
    private static readonly string[] AllowedActions = ["Apologize", "Fight"];

    protected override IGameClientCall Map(TheftEncounterStartedEvent gameEvent) =>
        new GameClientCall<TheftEncounterState>(
            new TheftEncounterState(
                gameEvent.Encounter.Id,
                gameEvent.Encounter.ConfrontingName,
                gameEvent.Encounter.ItemNames.ToArray(),
                AllowedActions
            ),
            static (client, arguments) => client.TheftEncounterStarted(arguments)
        );
}
