using TRPG.Application.Configuration;
using TRPG.Domain;

namespace TRPG.Application.CreatureFormulas;

public sealed record RegenVitals(
    int CurrentHp,
    int MaximumHp,
    int CurrentAp,
    int MaximumAp,
    int CurrentMp,
    int MaximumMp,
    GameInstant LastRegenAt
);

public static class VitalsRegeneration
{
    public static RegenVitals? Regenerate(
        RegenVitals vitals,
        GameInstant now,
        CreatureRegenOptions options
    )
    {
        if (options.TickInterval <= TimeSpan.Zero)
        {
            throw new InvalidOperationException("The regeneration tick interval must be positive.");
        }

        var completeTicks = (long)Math.Floor((now - vitals.LastRegenAt) / options.TickInterval);
        if (completeTicks <= 0)
        {
            return null;
        }

        return vitals with
        {
            CurrentHp = Regen(
                vitals.CurrentHp,
                vitals.MaximumHp,
                options.HpRegenPercentPerTick,
                completeTicks
            ),
            CurrentAp = Regen(
                vitals.CurrentAp,
                vitals.MaximumAp,
                options.ApRegenPercentPerTick,
                completeTicks
            ),
            CurrentMp = Regen(
                vitals.CurrentMp,
                vitals.MaximumMp,
                options.MpRegenPercentPerTick,
                completeTicks
            ),
            // Advancing by whole ticks keeps the partial tick banked toward the next one.
            LastRegenAt = vitals.LastRegenAt + options.TickInterval * completeTicks,
        };
    }

    private static int Regen(int current, int maximum, float percentPerTick, long ticks)
    {
        var missing = maximum - current;
        if (missing <= 0)
        {
            return current;
        }

        var amount = Math.Round(
            maximum * (double)percentPerTick * ticks,
            MidpointRounding.AwayFromZero
        );
        var minimumAmount = percentPerTick > 0 ? 1 : 0;
        return current + (int)Math.Min(missing, Math.Max(amount, minimumAmount));
    }
}
