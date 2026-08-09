using TRPG.Application.Combat;
using CombatBlockEvent = TRPG.Contracts.Combat.Responses.CombatBlockEvent;
using CombatHitEvent = TRPG.Contracts.Combat.Responses.CombatHitEvent;
using CombatMissEvent = TRPG.Contracts.Combat.Responses.CombatMissEvent;
using CombatRegeneratedEvent = TRPG.Contracts.Combat.Responses.CombatRegeneratedEvent;
using CombatResourceStateUpdatedEvent = TRPG.Contracts.Combat.Responses.CombatResourceStateUpdatedEvent;
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
            )
            {
                Narration = CombatNarration.Describe(hit),
            },
            Miss miss => new CombatMissEvent(
                AttackerId: miss.AttackerId,
                AttackerName: miss.AttackerName,
                AbilityName: miss.AbilityName,
                TargetId: miss.TargetId,
                TargetName: miss.TargetName
            )
            {
                Narration = CombatNarration.Describe(miss),
            },
            Block block => new CombatBlockEvent(
                AttackerId: block.AttackerId,
                AttackerName: block.AttackerName,
                AbilityName: block.AbilityName,
                TargetId: block.TargetId,
                TargetName: block.TargetName
            )
            {
                Narration = CombatNarration.Describe(block),
            },
            Regenerated regenerated => new CombatRegeneratedEvent(
                AttackerId: regenerated.CombatantId,
                AttackerName: regenerated.CombatantName,
                PreviousAp: regenerated.PreviousAp,
                CurrentAp: regenerated.CurrentAp,
                MaximumAp: regenerated.MaximumAp,
                PreviousMp: regenerated.PreviousMp,
                CurrentMp: regenerated.CurrentMp,
                MaximumMp: regenerated.MaximumMp
            ),
            ResourceStateUpdated resourceStateUpdated => new CombatResourceStateUpdatedEvent(
                AttackerId: resourceStateUpdated.CombatantId,
                AttackerName: resourceStateUpdated.CombatantName,
                CurrentAp: resourceStateUpdated.CurrentAp,
                MaximumAp: resourceStateUpdated.MaximumAp,
                CurrentMp: resourceStateUpdated.CurrentMp,
                MaximumMp: resourceStateUpdated.MaximumMp
            ),
            _ => null,
        };
}
