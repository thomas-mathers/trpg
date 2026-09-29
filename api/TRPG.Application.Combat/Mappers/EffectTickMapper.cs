using TRPG.Application.Combat.Events;
using TRPG.Application.Effects.Results;

namespace TRPG.Application.Combat.Mappers;

internal static class EffectTickMapper
{
    public static CombatResolution ToCombatResolution(this EffectTick tick, Combatant target) =>
        tick.Match(
            damage => damage.ToCombatResolution(target),
            healing => healing.ToCombatResolution(target)
        );
}
