using TRPG.Application.Combat.Results;
using ContractCombatantState = TRPG.Combat.Responses.CombatantState;

namespace TRPG.Combat.Mappers;

internal static class CombatantMapper
{
    public static ContractCombatantState ToContract(this CombatantResult combatant) =>
        new(
            Id: combatant.Id,
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
