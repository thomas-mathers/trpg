using TRPG.Application.Encounters.Events;
using TRPG.Encounters.Responses;

namespace TRPG.GameSessions.Hubs;

internal sealed class ShakedownEncounterResolvedEventMapper
    : GameClientEventMapper<ShakedownEncounterResolvedEvent>
{
    protected override IGameClientCall Map(ShakedownEncounterResolvedEvent gameEvent) =>
        new GameClientCall<ShakedownEncounterResolutionFact>(
            new ShakedownEncounterResolutionFact(
                gameEvent.Fact.EncounterId,
                (ShakedownEncounterResolutionOutcome)gameEvent.Fact.Outcome,
                gameEvent.Fact.FactionName,
                gameEvent.Fact.LocationName,
                gameEvent.Fact.TollAmount,
                gameEvent.Fact.MemberNames
            ),
            static (client, arguments) => client.ShakedownEncounterResolved(arguments)
        );
}
