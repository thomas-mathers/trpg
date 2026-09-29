using Microsoft.Extensions.Options;
using TRPG.Application.Abilities;
using TRPG.Application.Configuration;
using TRPG.Application.CreatureFormulas;
using TRPG.Application.Effects.Results;
using TRPG.Domain;

namespace TRPG.Application.Effects;

public class EffectAdvancer(
    IOptionsSnapshot<CombatOptions> optionsSnapshot,
    EffectTimeScale timeScale
)
{
    private sealed record OwedTick(GameInstant At, Func<EffectTick> Apply);

    public EffectAdvanceResult Advance(EffectState state, GameInstant now)
    {
        var previousCount = CountEffects(state);
        var events = state.IsAlive ? ApplyOwedTicks(state, now) : [];

        RemoveExpired(state, now);

        return new EffectAdvanceResult(
            events.Count > 0 || previousCount != CountEffects(state),
            events
        );
    }

    private List<EffectTick> ApplyOwedTicks(EffectState state, GameInstant now)
    {
        var events = new List<EffectTick>();

        foreach (var tick in ConsumeOwedTicks(state, now).OrderBy(tick => tick.At))
        {
            events.Add(tick.Apply());

            if (!state.IsAlive)
            {
                break;
            }
        }

        return events;
    }

    private List<OwedTick> ConsumeOwedTicks(EffectState state, GameInstant now)
    {
        var ticks = new List<OwedTick>();

        foreach (var hot in state.ActiveHots)
        {
            var count = CountOwedTicks(hot.NextTickAt, hot.ExpiresAt, now);
            ticks.AddRange(
                Enumerable
                    .Range(0, count)
                    .Select(index => new OwedTick(
                        hot.NextTickAt + timeScale.Round * index,
                        () => ApplyHotTick(state, hot)
                    ))
            );
            hot.NextTickAt += timeScale.Round * count;
        }

        foreach (var dot in state.ActiveDots)
        {
            var count = CountOwedTicks(dot.NextTickAt, dot.ExpiresAt, now);
            ticks.AddRange(
                Enumerable
                    .Range(0, count)
                    .Select(index => new OwedTick(
                        dot.NextTickAt + timeScale.Round * index,
                        () => ApplyDotTick(state, dot)
                    ))
            );
            dot.NextTickAt += timeScale.Round * count;
        }

        return ticks;
    }

    private int CountOwedTicks(GameInstant nextTickAt, GameInstant expiresAt, GameInstant now)
    {
        var lastEligible = expiresAt < now ? expiresAt : now;

        return nextTickAt > lastEligible
            ? 0
            : (int)((lastEligible - nextTickAt) / timeScale.Round) + 1;
    }

    private EffectTick ApplyDotTick(EffectState state, ActiveDot dot)
    {
        var damage = DamageMitigation.Calculate(
            dot.Amount,
            state.ResistanceFor(dot.DamageType),
            optionsSnapshot.Value.MaxResistancePercent
        );

        state.CurrentHp = Math.Max(state.CurrentHp - damage, 0);

        return new DamageEffectTick(
            dot.AbilityName,
            dot.DamageType,
            damage,
            state.CurrentHp,
            state.MaximumHp,
            !state.IsAlive
        );
    }

    private static EffectTick ApplyHotTick(EffectState state, ActiveHot hot)
    {
        state.CurrentHp = Math.Min(state.CurrentHp + hot.Amount, state.MaximumHp);

        return new HealingEffectTick(hot.AbilityName, hot.Amount, state.CurrentHp, state.MaximumHp);
    }

    private static int CountEffects(EffectState state) =>
        state.ActiveDots.Count
        + state.ActiveHots.Count
        + state.ActiveBuffs.Count
        + state.ActiveConditions.Count
        + state.CooldownReadyAtByAbility.Count;

    private static void RemoveExpired(EffectState state, GameInstant now)
    {
        state.ActiveDots.RemoveAll(dot => dot.ExpiresAt <= now);
        state.ActiveHots.RemoveAll(hot => hot.ExpiresAt <= now);
        state.ActiveBuffs.RemoveAll(buff => buff.ExpiresAt <= now);

        foreach (var condition in state.ActiveConditions.Where(c => c.Value <= now).ToArray())
        {
            state.ActiveConditions.Remove(condition.Key);
        }

        foreach (
            var cooldown in state.CooldownReadyAtByAbility.Where(c => c.Value <= now).ToArray()
        )
        {
            state.CooldownReadyAtByAbility.Remove(cooldown.Key);
        }
    }
}
