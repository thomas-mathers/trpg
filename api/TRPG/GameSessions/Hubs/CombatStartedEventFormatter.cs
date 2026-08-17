using TRPG.Application.Combat.Events;
using TRPG.Combat.Mappers;

namespace TRPG.GameSessions.Hubs;

internal sealed class CombatStartedEventFormatter : GameClientEventFormatter<CombatStartedEvent>
{
    protected override Task Dispatch(IGameClient client, CombatStartedEvent gameEvent) =>
        client.CombatStarted(gameEvent.Combatants.ToCombatantStates());
}
