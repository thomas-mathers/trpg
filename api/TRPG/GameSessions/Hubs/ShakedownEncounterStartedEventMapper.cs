using TRPG.Application.Encounters.Events;
using TRPG.Encounters.Responses;
using ContractCreatureType = TRPG.GameSessions.Responses.CreatureType;

namespace TRPG.GameSessions.Hubs;

internal sealed class ShakedownEncounterStartedEventMapper
    : GameClientEventMapper<ShakedownEncounterStartedEvent>
{
    private static readonly string[] AllowedActions = ["Intimidate", "PayToll", "Fight", "Flee"];

    protected override IGameClientCall Map(ShakedownEncounterStartedEvent gameEvent) =>
        new GameClientCall<ShakedownEncounterState>(
            new ShakedownEncounterState(
                gameEvent.Encounter.Id,
                gameEvent.Encounter.FactionName,
                gameEvent.Encounter.LocationName!,
                gameEvent.Encounter.TollAmount,
                gameEvent
                    .Encounter.Members.Select(member => new HostileEncounterMemberState(
                        member.Name,
                        (ContractCreatureType)member.CreatureType,
                        member.Level
                    ))
                    .ToArray(),
                AllowedActions,
                gameEvent.CanAffordToll
            ),
            static (client, arguments) => client.ShakedownEncounterStarted(arguments)
        );
}
