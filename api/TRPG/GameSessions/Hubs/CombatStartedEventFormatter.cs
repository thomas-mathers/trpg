using TRPG.Application.Combat.Events;
using TRPG.Combat.Mappers;

namespace TRPG.GameSessions.Hubs;

internal sealed class CombatStartedEventFormatter : GameClientEventFormatter<CombatStartedEvent>
{
    protected override GameClientMessage Format(CombatStartedEvent gameEvent) =>
        new("CombatStarted", gameEvent.Combatants.ToCombatantStates());
}
