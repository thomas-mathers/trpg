using TRPG.Application.Encounters.Events;
using TRPG.Combat.Mappers;
using TRPG.Combat.Responses;

namespace TRPG.GameSessions.Hubs;

internal sealed class CombatUpdatedEventMapper : GameClientEventMapper<CombatUpdatedEvent>
{
    protected override IGameClientCall Map(CombatUpdatedEvent gameEvent) =>
        new GameClientCall<CombatUpdated>(
            new CombatUpdated(
                gameEvent.Combatants.ToCombatantStates(),
                gameEvent.Events.ToCombatActionResults(),
                gameEvent.Events.ToCombatMessages(),
                gameEvent.Events.ToCombatRegenerations(),
                gameEvent.Events.ToCombatResourceStates(),
                gameEvent.Outcome.ToContract()
            ),
            static (client, arguments) => client.CombatUpdated(arguments)
        );
}
