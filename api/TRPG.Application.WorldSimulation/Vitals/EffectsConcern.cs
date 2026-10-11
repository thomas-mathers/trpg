using TRPG.Application.Creatures.Results;
using TRPG.Application.WorldSimulation.Movement;
using TRPG.Domain;

namespace TRPG.Application.WorldSimulation.Vitals;

internal static class EffectsConcern
{
    public static IReadOnlyList<CreatureEffectsAdvanced> Tick(
        IEnumerable<CreatureEffectAdvance> advances,
        GameInstant now
    ) =>
        [
            .. advances.Select(advance => new CreatureEffectsAdvanced(
                advance.Vitals.CreatureId,
                now,
                advance.LocationId,
                advance.Vitals,
                advance.Update
            )),
        ];

    public static IEnumerable<CreatureVitalState> Settle(
        IEnumerable<CreatureVitalState> states,
        IReadOnlyList<CreatureEffectsAdvanced> effects
    )
    {
        var effectsByCreatureId = effects.ToDictionary(effect => effect.CreatureId);

        return states
            .Where(state =>
                !effectsByCreatureId.TryGetValue(state.Vitals.CreatureId, out var effect)
                || effect.Update.IsAlive
            )
            .Select(state =>
                effectsByCreatureId.TryGetValue(state.Vitals.CreatureId, out var effect)
                    ? state with
                    {
                        Vitals = effect.Vitals,
                    }
                    : state
            );
    }
}
