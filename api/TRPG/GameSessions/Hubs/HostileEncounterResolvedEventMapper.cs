using TRPG.Application.Encounters.Events;
using TRPG.Encounters.Responses;

namespace TRPG.GameSessions.Hubs;

internal sealed class HostileEncounterResolvedEventMapper : GameClientEventMapper<HostileEncounterResolvedEvent>
{
    protected override IGameClientCall Map(HostileEncounterResolvedEvent gameEvent) =>
        new GameClientCall<HostileEncounterResolutionFact>(
            new HostileEncounterResolutionFact(
                gameEvent.Fact.EncounterId,
                (HostileEncounterResolutionOutcome)gameEvent.Fact.Outcome,
                gameEvent.Fact.FactionName,
                gameEvent.Fact.LocationName,
                gameEvent.Fact.MemberNames
            ),
            static (client, arguments) => client.HostileEncounterResolved(arguments)
        );
}
