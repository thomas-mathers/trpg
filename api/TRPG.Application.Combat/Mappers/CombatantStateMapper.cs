using TRPG.Application.Combat;
using ContractCombatantState = TRPG.Application.Combat.ClientEvents.CombatantState;

namespace TRPG.Application.Combat.Mappers;

internal static class CombatantStateMapper
{
    public static IReadOnlyCollection<ContractCombatantState> ToCombatantStates(
        this IReadOnlyList<Combatant> combatants
    ) =>
        combatants
            .OrderByDescending(combatant => combatant.TurnOrder)
            .Select(combatant => combatant.ToContract())
            .ToArray();
}
