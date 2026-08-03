using TRPG.Application.Combat;
using CombatBlockEvent = TRPG.Contracts.Combat.Responses.CombatBlockEvent;
using CombatHitEvent = TRPG.Contracts.Combat.Responses.CombatHitEvent;
using CombatMissEvent = TRPG.Contracts.Combat.Responses.CombatMissEvent;
using CombatRoundEvent = TRPG.Contracts.Combat.Responses.CombatRoundEvent;

namespace TRPG.Application.Common.Mappers;

public static class CombatRoundEventMapper
{
    public static IReadOnlyList<CombatRoundEvent> ToCombatRoundEvents(
        IReadOnlyList<CombatEvent> events
    ) => events.Select(ToCombatRoundEvent).OfType<CombatRoundEvent>().ToArray();

    private static CombatRoundEvent? ToCombatRoundEvent(CombatEvent combatEvent) =>
        combatEvent switch
        {
            Hit hit => new CombatHitEvent(
                AttackerId: hit.AttackerId,
                AttackerName: hit.AttackerName,
                AbilityName: hit.AbilityName,
                TargetId: hit.TargetId,
                TargetName: hit.TargetName,
                Damage: hit.Damage,
                DamageType: hit.DamageType.ToContract(),
                IsCritical: hit.IsCritical,
                Killed: hit.Killed,
                TargetRemainingHp: hit.TargetRemainingHp,
                TargetMaximumHp: hit.TargetMaximumHp,
                AppliedConditions: hit.AppliedConditions.Select(c => c.ToContract()).ToArray()
            ),
            Miss miss => new CombatMissEvent(
                AttackerId: miss.AttackerId,
                AttackerName: miss.AttackerName,
                AbilityName: miss.AbilityName,
                TargetId: miss.TargetId,
                TargetName: miss.TargetName
            ),
            Block block => new CombatBlockEvent(
                AttackerId: block.AttackerId,
                AttackerName: block.AttackerName,
                AbilityName: block.AbilityName,
                TargetId: block.TargetId,
                TargetName: block.TargetName
            ),
            _ => null,
        };
}
