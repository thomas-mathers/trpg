using TRPG.Application.Encounters.Events;
using TRPG.Encounters.Responses;

namespace TRPG.GameSessions.Hubs;

internal sealed class SuspicionEncounterResolvedEventMapper
    : GameClientEventMapper<SuspicionEncounterResolvedEvent>
{
    protected override IGameClientCall Map(SuspicionEncounterResolvedEvent gameEvent) =>
        new GameClientCall<SuspicionEncounterResolutionFact>(
            new SuspicionEncounterResolutionFact(
                gameEvent.Fact.EncounterId,
                (SuspicionEncounterResolutionOutcome)gameEvent.Fact.Outcome,
                gameEvent.Fact.GuardName,
                gameEvent.Fact.LocationName,
                gameEvent.Fact.EscalatedGuardEncounterId
            ),
            static (client, arguments) => client.SuspicionEncounterResolved(arguments)
        );
}
