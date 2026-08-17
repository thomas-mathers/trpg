using TRPG.Application.Combat.Events;
using TRPG.Combat.ClientModels;
using TRPG.Combat.Mappers;

namespace TRPG.GameSessions.Hubs;

internal sealed class CombatUpdatedEventFormatter : GameClientEventFormatter<CombatUpdatedEvent>
{
    protected override Task Dispatch(IGameClient client, CombatUpdatedEvent gameEvent) =>
        client.CombatUpdated(
            new CombatUpdatePayload(
                gameEvent.Combatants.ToCombatantStates(),
                gameEvent.Events.ToCombatRoundEntries(),
                gameEvent.Outcome.ToContract()
            )
        );
}
