using TRPG.Application.Creatures.Commands;
using TRPG.Application.Creatures.Results;
using TRPG.Application.Effects;
using TRPG.Application.Effects.Mappers;

namespace TRPG.Application.Creatures.Mappers;

internal static class EffectStateMapper
{
    public static CreatureStateUpdate ToCreatureStateUpdate(
        this EffectState state,
        CreatureVitals vitals
    ) =>
        new(
            CreatureId: vitals.CreatureId,
            CurrentHp: state.CurrentHp,
            CurrentAp: vitals.CurrentAp,
            CurrentMp: vitals.CurrentMp,
            IsAlive: state.IsAlive,
            ActiveConditions: state.ActiveConditions.ToDictionary(
                pair => pair.Key.ToString(),
                pair => pair.Value
            ),
            CooldownReadyAtByAbility: state.CooldownReadyAtByAbility,
            ActiveDots: state.ActiveDots.Select(effect => effect.ToModel()).ToArray(),
            ActiveHots: state.ActiveHots.Select(effect => effect.ToModel()).ToArray(),
            ActiveBuffs: state.ActiveBuffs.Select(effect => effect.ToModel()).ToArray()
        );
}
