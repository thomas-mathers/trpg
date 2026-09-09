using TRPG.Application.Encounters.Events;
using TRPG.Encounters.Responses;

namespace TRPG.GameSessions.Hubs;

internal sealed class TrapEncounterResolvedEventMapper
    : GameClientEventMapper<TrapEncounterResolvedEvent>
{
    protected override IGameClientCall Map(TrapEncounterResolvedEvent gameEvent) =>
        new GameClientCall<TrapEncounterResolutionFact>(
            new TrapEncounterResolutionFact(
                gameEvent.Fact.EncounterId,
                (TrapEncounterResolutionOutcome)gameEvent.Fact.Outcome,
                (TrapKind)gameEvent.Fact.TrapKind,
                gameEvent.Fact.TargetLocationName
            ),
            static (client, arguments) => client.TrapEncounterResolved(arguments)
        );
}
