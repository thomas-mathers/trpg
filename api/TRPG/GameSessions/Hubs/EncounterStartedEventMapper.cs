using TRPG.Application.Encounters.Events;
using TRPG.Encounters.Responses;
using ContractCreatureType = TRPG.GameSessions.Responses.CreatureType;

namespace TRPG.GameSessions.Hubs;

internal sealed class EncounterStartedEventMapper : GameClientEventMapper<EncounterStartedEvent>
{
    protected override IGameClientCall Map(EncounterStartedEvent gameEvent) =>
        new GameClientCall<HostileEncounterState>(
            new HostileEncounterState(
                gameEvent.State.EncounterId,
                gameEvent.State.FactionName,
                gameEvent.State.LocationName,
                gameEvent
                    .State.Members.Select(member => new HostileEncounterMemberState(
                        member.Name,
                        (ContractCreatureType)member.CreatureType,
                        member.Level
                    ))
                    .ToArray(),
                gameEvent.State.AllowedActions
            ),
            static (client, arguments) => client.EncounterStarted(arguments)
        );
}
