using TRPG.Application.Combat.Events;
using TRPG.Application.Effects.Results;

namespace TRPG.Application.Combat.Mappers;

internal static class DamageEffectTickMapper
{
    public static DamageTicked ToCombatResolution(this DamageEffectTick tick, Combatant target) =>
        new(
            CreatureName: target.Name,
            AbilityName: tick.AbilityName,
            DamageType: tick.DamageType,
            Damage: tick.Damage,
            RemainingHp: tick.RemainingHp,
            MaximumHp: tick.MaximumHp,
            Killed: tick.Killed
        );
}
