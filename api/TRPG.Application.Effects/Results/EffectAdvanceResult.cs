using TRPG.Domain.Models;

namespace TRPG.Application.Effects.Results;

public sealed record EffectAdvanceResult(bool Changed, IReadOnlyList<EffectTick> Ticks);

public abstract record EffectTick(string AbilityName, int RemainingHp, int MaximumHp)
{
    public abstract TResult Match<TResult>(
        Func<DamageEffectTick, TResult> mapDamage,
        Func<HealingEffectTick, TResult> mapHealing
    );
}

public sealed record DamageEffectTick(
    string AbilityName,
    DamageType DamageType,
    int Damage,
    int RemainingHp,
    int MaximumHp,
    bool Killed
) : EffectTick(AbilityName, RemainingHp, MaximumHp)
{
    public override TResult Match<TResult>(
        Func<DamageEffectTick, TResult> mapDamage,
        Func<HealingEffectTick, TResult> mapHealing
    ) => mapDamage(this);
}

public sealed record HealingEffectTick(
    string AbilityName,
    int Amount,
    int RemainingHp,
    int MaximumHp
) : EffectTick(AbilityName, RemainingHp, MaximumHp)
{
    public override TResult Match<TResult>(
        Func<DamageEffectTick, TResult> mapDamage,
        Func<HealingEffectTick, TResult> mapHealing
    ) => mapHealing(this);
}
