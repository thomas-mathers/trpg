using TRPG.Application.Abilities;
using TRPG.Application.Combat.Events;
using TRPG.Domain;

namespace TRPG.Application.Combat;

public class EffectAdvancer(DamageCalculator damageCalculator, CombatTimeScale timeScale)
{
    private sealed record OwedTick(GameInstant At, Func<CombatResolution> Apply);

    public IReadOnlyList<CombatResolution> Advance(Combatant combatant, GameInstant now)
    {
        var events = combatant.IsAlive ? ApplyOwedTicks(combatant, now) : [];

        RemoveExpired(combatant, now);

        return events;
    }

    private List<CombatResolution> ApplyOwedTicks(Combatant combatant, GameInstant now)
    {
        var events = new List<CombatResolution>();

        foreach (var tick in ConsumeOwedTicks(combatant, now).OrderBy(tick => tick.At))
        {
            events.Add(tick.Apply());

            if (!combatant.IsAlive)
            {
                break;
            }
        }

        return events;
    }

    private List<OwedTick> ConsumeOwedTicks(Combatant combatant, GameInstant now)
    {
        var ticks = new List<OwedTick>();

        foreach (var hot in combatant.ActiveHots)
        {
            var count = CountOwedTicks(hot.NextTickAt, hot.ExpiresAt, now);
            ticks.AddRange(
                Enumerable
                    .Range(0, count)
                    .Select(index => new OwedTick(
                        hot.NextTickAt + timeScale.Round * index,
                        () => ApplyHotTick(combatant, hot)
                    ))
            );
            hot.NextTickAt += timeScale.Round * count;
        }

        foreach (var dot in combatant.ActiveDots)
        {
            var count = CountOwedTicks(dot.NextTickAt, dot.ExpiresAt, now);
            ticks.AddRange(
                Enumerable
                    .Range(0, count)
                    .Select(index => new OwedTick(
                        dot.NextTickAt + timeScale.Round * index,
                        () => ApplyDotTick(combatant, dot)
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

    private CombatResolution ApplyDotTick(Combatant combatant, ActiveDot dot)
    {
        var damage = damageCalculator.CalculateDamage(dot.Amount, dot.DamageType, combatant);

        combatant.CurrentHp = Math.Max(combatant.CurrentHp - damage, 0);

        return new DamageTicked(
            combatant.Name,
            dot.AbilityName,
            dot.DamageType,
            damage,
            combatant.CurrentHp,
            combatant.MaximumHp,
            !combatant.IsAlive
        );
    }

    private static CombatResolution ApplyHotTick(Combatant combatant, ActiveHot hot)
    {
        combatant.CurrentHp = Math.Min(combatant.CurrentHp + hot.Amount, combatant.MaximumHp);

        return new Healed(
            combatant.CreatureId,
            combatant.Name,
            hot.AbilityName,
            combatant.CreatureId,
            combatant.Name,
            hot.Amount,
            combatant.CurrentHp,
            combatant.MaximumHp
        );
    }

    private static void RemoveExpired(Combatant combatant, GameInstant now)
    {
        combatant.ActiveDots.RemoveAll(dot => dot.ExpiresAt <= now);
        combatant.ActiveHots.RemoveAll(hot => hot.ExpiresAt <= now);
        combatant.ActiveBuffs.RemoveAll(buff => buff.ExpiresAt <= now);

        foreach (var condition in combatant.ActiveConditions.Where(c => c.Value <= now).ToArray())
        {
            combatant.ActiveConditions.Remove(condition.Key);
        }

        foreach (
            var cooldown in combatant.CooldownReadyAtByAbility.Where(c => c.Value <= now).ToArray()
        )
        {
            combatant.CooldownReadyAtByAbility.Remove(cooldown.Key);
        }
    }
}
