using TRPG.Application.Encounters.Events;
using TRPG.Encounters.Responses;

namespace TRPG.GameSessions.Hubs;

internal sealed class TheftEncounterResolvedEventMapper
    : GameClientEventMapper<TheftEncounterResolvedEvent>
{
    protected override IGameClientCall Map(TheftEncounterResolvedEvent gameEvent) =>
        new GameClientCall<TheftEncounterResolutionFact>(
            new TheftEncounterResolutionFact(
                gameEvent.Fact.EncounterId,
                (TheftEncounterResolutionOutcome)gameEvent.Fact.Outcome,
                gameEvent.Fact.ConfrontingName,
                gameEvent.Fact.ItemNames,
                gameEvent.Fact.ItemsReturned
            ),
            static (client, arguments) => client.TheftEncounterResolved(arguments)
        );
}
