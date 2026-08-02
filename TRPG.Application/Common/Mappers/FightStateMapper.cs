using TRPG.Application.Combat;
using ActiveBuff = TRPG.Contracts.Combat.Responses.ActiveBuff;
using ActiveDot = TRPG.Contracts.Combat.Responses.ActiveDot;
using ActiveHot = TRPG.Contracts.Combat.Responses.ActiveHot;
using CombatantState = TRPG.Contracts.Combat.Responses.CombatantState;
using FightState = TRPG.Contracts.Combat.Responses.FightState;

namespace TRPG.Application.Common.Mappers;

public static class FightStateMapper
{
    public static FightState ToFightState(IReadOnlyList<Combatant> combatants) =>
        new(
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
                    ActiveConditions: c.ActiveConditions.Where(kv => kv.Value > 0)
                        .ToDictionary(kv => kv.Key.ToContract(), kv => kv.Value),
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
                .ToArray()
        );
}
