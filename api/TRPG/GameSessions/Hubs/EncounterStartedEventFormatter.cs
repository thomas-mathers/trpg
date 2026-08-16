using TRPG.Application.Common.Events;
using TRPG.Application.Encounters.Events;
using TRPG.Encounters.Responses;
using ContractCreatureType = TRPG.Contracts.Scenes.Responses.CreatureType;

namespace TRPG.GameSessions.Hubs;

internal sealed class EncounterStartedEventFormatter
    : GameClientEventFormatter<EncounterStartedEvent>
{
    protected override GameClientMessage Format(EncounterStartedEvent gameEvent) =>
        new(
            gameEvent.MethodName,
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
            )
        );
}
