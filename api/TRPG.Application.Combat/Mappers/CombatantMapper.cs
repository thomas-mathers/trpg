using TRPG.Application.Combat;
using ContractCombatantState = TRPG.Application.Combat.Responses.CombatantState;

namespace TRPG.Application.Combat.Mappers;

internal static class CombatantMapper
{
    public static ContractCombatantState ToContract(this Combatant combatant) =>
        new(
            Id: combatant.CreatureId,
            Name: combatant.Name,
            Level: combatant.Level,
            IsPlayer: combatant.IsPlayer,
            IsAlive: combatant.IsAlive,
            CurrentHp: combatant.CurrentHp,
            MaximumHp: combatant.MaximumHp,
            CurrentAp: combatant.CurrentAp,
            MaximumAp: combatant.MaximumAp,
            CurrentMp: combatant.CurrentMp,
            MaximumMp: combatant.MaximumMp,
            ActiveConditions: combatant.ActiveConditions.ToContract(),
            ActiveDots: combatant.ActiveDots.Select(dot => dot.ToContract()).ToArray(),
            ActiveHots: combatant.ActiveHots.Select(hot => hot.ToContract()).ToArray(),
            ActiveBuffs: combatant.ActiveBuffs.Select(buff => buff.ToContract()).ToArray()
        );
}
