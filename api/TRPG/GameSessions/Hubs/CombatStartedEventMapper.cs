using TRPG.Application.Combat.Events;
using TRPG.Combat.Mappers;
using TRPG.Combat.Responses;

namespace TRPG.GameSessions.Hubs;

internal sealed class CombatStartedEventMapper : GameClientEventMapper<CombatStartedEvent>
{
    protected override IGameClientCall Map(CombatStartedEvent gameEvent) =>
        new GameClientCall<IReadOnlyCollection<CombatantState>>(
            gameEvent.Combatants.ToCombatantStates(),
            static (client, arguments) => client.CombatStarted(arguments)
        );
}
