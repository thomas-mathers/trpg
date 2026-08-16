using ContractCombatOutcome = TRPG.Combat.ClientModels.CombatOutcome;
using DataCombatOutcome = TRPG.Domain.Models.CombatOutcome;

namespace TRPG.Combat.Mappers;

internal static class CombatOutcomeMapper
{
    public static ContractCombatOutcome ToContract(this DataCombatOutcome outcome) =>
        outcome switch
        {
            DataCombatOutcome.Ongoing => ContractCombatOutcome.Ongoing,
            DataCombatOutcome.Victory => ContractCombatOutcome.Victory,
            DataCombatOutcome.Defeat => ContractCombatOutcome.Defeat,
            DataCombatOutcome.Fled => ContractCombatOutcome.Fled,
            _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, null),
        };
}
