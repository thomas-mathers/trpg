using TRPG.Application.Combat;
using ContractCombatantState = TRPG.Combat.ClientModels.CombatantState;

namespace TRPG.Combat.Mappers;

internal static class CombatantStateMapper
{
    public static IReadOnlyCollection<ContractCombatantState> ToCombatantStates(
        this IReadOnlyCollection<Combatant> combatants
    ) =>
        combatants
            .OrderByDescending(combatant => combatant.TurnOrder)
            .Select(combatant => combatant.ToContract())
            .ToArray();
}
