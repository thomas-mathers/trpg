using TRPG.Application.Encounters.Events;
using TRPG.Encounters.Responses;

namespace TRPG.GameSessions.Hubs;

internal sealed class EncounterResolvedEventFormatter
    : GameClientEventFormatter<EncounterResolvedEvent>
{
    protected override GameClientMessage Format(EncounterResolvedEvent gameEvent) =>
        new(
            "EncounterResolved",
            new EncounterResolutionFact(
                gameEvent.Fact.EncounterId,
                (EncounterResolutionOutcome)gameEvent.Fact.Outcome,
                gameEvent.Fact.FactionName,
                gameEvent.Fact.LocationName,
                gameEvent.Fact.MemberNames
            )
        );
}
