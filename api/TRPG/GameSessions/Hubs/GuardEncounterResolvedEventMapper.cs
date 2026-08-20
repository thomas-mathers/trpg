using TRPG.Application.Encounters.Events;
using TRPG.Encounters.Responses;

namespace TRPG.GameSessions.Hubs;

internal sealed class GuardEncounterResolvedEventMapper
    : GameClientEventMapper<GuardEncounterResolvedEvent>
{
    protected override IGameClientCall Map(GuardEncounterResolvedEvent gameEvent) =>
        new GameClientCall<GuardEncounterResolutionFact>(
            new GuardEncounterResolutionFact(
                gameEvent.Fact.EncounterId,
                (GuardEncounterResolutionOutcome)gameEvent.Fact.Outcome,
                gameEvent.Fact.GuardName,
                gameEvent.Fact.LocationName,
                gameEvent.Fact.FineAmount,
                gameEvent.Fact.JailHours
            ),
            static (client, arguments) => client.GuardEncounterResolved(arguments)
        );
}
