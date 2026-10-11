using TRPG.Application.Configuration;
using TRPG.Application.CreatureFormulas;
using TRPG.Application.Creatures.Results;
using TRPG.Application.WorldSimulation.Movement;
using TRPG.Domain;

namespace TRPG.Application.WorldSimulation.Vitals;

internal static class RegenerationConcern
{
    public static IReadOnlyList<SimEvent> Tick(
        IEnumerable<CreatureVitalState> states,
        GameInstant now,
        CreatureRegenOptions options
    ) => [.. states.Select(state => Regenerate(state, now, options)).OfType<SimEvent>()];

    private static CreatureRegenerated? Regenerate(
        CreatureVitalState state,
        GameInstant now,
        CreatureRegenOptions options
    )
    {
        var vitals = state.Vitals;
        var regenerated = VitalsRegeneration.Regenerate(
            new RegenVitals(
                vitals.CurrentHp,
                vitals.MaximumHp,
                vitals.CurrentAp,
                vitals.MaximumAp,
                vitals.CurrentMp,
                vitals.MaximumMp,
                state.LastRegenAt
            ),
            now,
            options
        );

        return regenerated is null
            ? null
            : new CreatureRegenerated(
                vitals.CreatureId,
                now,
                state.LocationId,
                vitals with
                {
                    CurrentHp = regenerated.CurrentHp,
                    CurrentAp = regenerated.CurrentAp,
                    CurrentMp = regenerated.CurrentMp,
                },
                regenerated.LastRegenAt
            );
    }
}
