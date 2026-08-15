using TRPG.Application.Combat;
using ConditionType = TRPG.Application.Abilities.ConditionType;
using ContractActiveBuff = TRPG.Contracts.Combat.Responses.ActiveBuff;
using ContractActiveConditions = TRPG.Contracts.Combat.Responses.ActiveConditions;
using ContractActiveDot = TRPG.Contracts.Combat.Responses.ActiveDot;
using ContractActiveHot = TRPG.Contracts.Combat.Responses.ActiveHot;
using ContractCombatantState = TRPG.Contracts.Combat.Responses.CombatantState;

namespace TRPG.Application.Combat.Mappers;

internal static class CombatantStateMapper
{
    public static IReadOnlyCollection<ContractCombatantState> ToCombatantStates(
        IReadOnlyList<Combatant> combatants
    ) =>
        combatants
            .OrderByDescending(c => c.TurnOrder)
            .Select(c => new ContractCombatantState(
                Id: c.CreatureId,
                Name: c.Name,
                Level: c.Level,
                IsPlayer: c.IsPlayer,
                IsAlive: c.IsAlive,
                CurrentHp: c.CurrentHp,
                MaximumHp: c.MaximumHp,
                CurrentAp: c.CurrentAp,
                MaximumAp: c.MaximumAp,
                CurrentMp: c.CurrentMp,
                MaximumMp: c.MaximumMp,
                ActiveConditions: ToActiveConditions(c.ActiveConditions),
                ActiveDots: c.ActiveDots.Select(d => new ContractActiveDot(
                        d.AbilityName,
                        d.Amount,
                        d.DamageType.ToContract(),
                        d.RemainingTurns
                    ))
                    .ToArray(),
                ActiveHots: c.ActiveHots.Select(h => new ContractActiveHot(
                        h.AbilityName,
                        h.Amount,
                        h.RemainingTurns
                    ))
                    .ToArray(),
                ActiveBuffs: c.ActiveBuffs.Select(b => new ContractActiveBuff(
                        b.AbilityName,
                        b.Attribute.ToContract(),
                        b.Amount,
                        b.AmountType.ToContract(),
                        b.RemainingTurns
                    ))
                    .ToArray()
            ))
            .ToArray();

    private static ContractActiveConditions ToActiveConditions(
        IReadOnlyDictionary<ConditionType, int> conditions
    ) =>
        new()
        {
            Blinded = GetValue(conditions, ConditionType.Blinded),
            Bleeding = GetValue(conditions, ConditionType.Bleeding),
            Burning = GetValue(conditions, ConditionType.Burning),
            Disarmed = GetValue(conditions, ConditionType.Disarmed),
            Frozen = GetValue(conditions, ConditionType.Frozen),
            Poisoned = GetValue(conditions, ConditionType.Poisoned),
            Silenced = GetValue(conditions, ConditionType.Silenced),
            Snared = GetValue(conditions, ConditionType.Snared),
            Stunned = GetValue(conditions, ConditionType.Stunned),
        };

    private static int GetValue(
        IReadOnlyDictionary<ConditionType, int> conditions,
        ConditionType condition
    )
    {
        return conditions.GetValueOrDefault(condition);
    }
}
