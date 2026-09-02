using TRPG.Application.Encounters.Events;
using TRPG.Combat.Mappers;
using TRPG.Combat.Responses;

namespace TRPG.GameSessions.Hubs;

internal sealed class CombatStartedEventMapper : GameClientEventMapper<CombatStartedEvent>
{
    protected override IGameClientCall Map(CombatStartedEvent gameEvent) =>
        new GameClientCall<CombatStarted>(
            new CombatStarted(gameEvent.FightId, gameEvent.Combatants.ToCombatantStates()),
            static (client, arguments) => client.CombatStarted(arguments)
        );
}
