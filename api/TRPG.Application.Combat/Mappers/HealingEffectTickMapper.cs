using TRPG.Application.Combat.Events;
using TRPG.Application.Effects.Results;

namespace TRPG.Application.Combat.Mappers;

internal static class HealingEffectTickMapper
{
    public static Healed ToCombatResolution(this HealingEffectTick tick, Combatant target) =>
        new(
            SourceId: target.CreatureId,
            SourceName: target.Name,
            AbilityName: tick.AbilityName,
            TargetId: target.CreatureId,
            TargetName: target.Name,
            Amount: tick.Amount,
            TargetRemainingHp: tick.RemainingHp,
            TargetMaximumHp: tick.MaximumHp
        );
}
