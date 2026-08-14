using TRPG.Application.Combat;
using ActiveBuff = TRPG.Contracts.Combat.Responses.ActiveBuff;
using ActiveConditions = TRPG.Contracts.Combat.Responses.ActiveConditions;
using ActiveDot = TRPG.Contracts.Combat.Responses.ActiveDot;
using ActiveHot = TRPG.Contracts.Combat.Responses.ActiveHot;
using CombatantState = TRPG.Contracts.Combat.Responses.CombatantState;
using ConditionType = TRPG.Application.Abilities.ConditionType;

namespace TRPG.Application.Common.Mappers;

public static class CombatantStateMapper
{
    public static IReadOnlyCollection<CombatantState> ToCombatantStates(
        IReadOnlyList<Combatant> combatants
    ) =>
        combatants
            .OrderByDescending(c => c.TurnOrder)
            .Select(c => new CombatantState(
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
                ActiveDots: c.ActiveDots.Select(d => new ActiveDot(
                        d.AbilityName,
                        d.Amount,
                        d.DamageType.ToContract(),
                        d.RemainingTurns
                    ))
                    .ToArray(),
                ActiveHots: c.ActiveHots.Select(h => new ActiveHot(
                        h.AbilityName,
                        h.Amount,
                        h.RemainingTurns
                    ))
                    .ToArray(),
                ActiveBuffs: c.ActiveBuffs.Select(b => new ActiveBuff(
                        b.AbilityName,
                        b.Attribute.ToContract(),
                        b.Amount,
                        b.AmountType.ToContract(),
                        b.RemainingTurns
                    ))
                    .ToArray()
            ))
            .ToArray();

    private static ActiveConditions ToActiveConditions(
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
