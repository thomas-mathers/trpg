using Microsoft.Extensions.Options;
using TRPG.Application.Abilities;
using TRPG.Application.Configuration;

namespace TRPG.Application.Effects;

public class EffectTimeScale(IOptions<WorldClockOptions> options)
{
    public static EffectTimeScale Unscaled { get; } = new(Options.Create(new WorldClockOptions()));

    public TimeSpan Round => Scale(CombatTiming.Round);

    public TimeSpan Scale(TimeSpan duration) => duration * options.Value.TimeScale;
}
