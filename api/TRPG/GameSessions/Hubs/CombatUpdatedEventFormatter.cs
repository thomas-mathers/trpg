using TRPG.Application.Combat.Events;
using TRPG.Application.Common.Events;
using TRPG.Combat.ClientModels;
using TRPG.Combat.Mappers;

namespace TRPG.GameSessions.Hubs;

internal sealed class CombatUpdatedEventFormatter : GameClientEventFormatter<CombatUpdatedEvent>
{
    protected override GameClientMessage Format(CombatUpdatedEvent gameEvent) =>
        new(
            gameEvent.MethodName,
            new CombatUpdatePayload(
                gameEvent.Combatants.ToCombatantStates(),
                gameEvent.Events.ToCombatRoundEntries(),
                gameEvent.Outcome.ToContract()
            )
        );
}
