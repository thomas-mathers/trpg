using TRPG.Application.Encounters.Events;
using TRPG.Encounters.Responses;

namespace TRPG.GameSessions.Hubs;

internal sealed class EncounterResolvedEventMapper : GameClientEventMapper<EncounterResolvedEvent>
{
    protected override IGameClientCall Map(EncounterResolvedEvent gameEvent) =>
        new GameClientCall<EncounterResolutionFact>(
            new EncounterResolutionFact(
                gameEvent.Fact.EncounterId,
                (EncounterResolutionOutcome)gameEvent.Fact.Outcome,
                gameEvent.Fact.FactionName,
                gameEvent.Fact.LocationName,
                gameEvent.Fact.MemberNames
            ),
            static (client, arguments) => client.EncounterResolved(arguments)
        );
}
