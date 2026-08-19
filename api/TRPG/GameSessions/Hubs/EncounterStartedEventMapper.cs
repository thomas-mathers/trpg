using TRPG.Application.Encounters.Events;
using TRPG.Encounters.Responses;
using ContractCreatureType = TRPG.GameSessions.Responses.CreatureType;

namespace TRPG.GameSessions.Hubs;

internal sealed class EncounterStartedEventMapper : GameClientEventMapper<EncounterStartedEvent>
{
    private static readonly string[] AllowedActions = ["Attack", "Evade", "Retreat"];

    protected override IGameClientCall Map(EncounterStartedEvent gameEvent) =>
        new GameClientCall<HostileEncounterState>(
            new HostileEncounterState(
                gameEvent.Encounter.Id,
                gameEvent.Encounter.FactionName,
                gameEvent.Encounter.LocationName,
                gameEvent
                    .Encounter.Members.Select(member => new HostileEncounterMemberState(
                        member.Name,
                        (ContractCreatureType)member.CreatureType,
                        member.Level
                    ))
                    .ToArray(),
                AllowedActions
            ),
            static (client, arguments) => client.EncounterStarted(arguments)
        );
}
