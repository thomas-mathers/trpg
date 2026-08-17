using TRPG.Application.Combat.Results;
using ContractCombatantState = TRPG.Combat.Responses.CombatantState;

namespace TRPG.Combat.Mappers;

internal static class CombatantResultMapper
{
    public static IReadOnlyCollection<ContractCombatantState> ToCombatantStates(
        this IReadOnlyCollection<CombatantResult> combatants
    ) => combatants.Select(combatant => combatant.ToContract()).ToArray();
}
