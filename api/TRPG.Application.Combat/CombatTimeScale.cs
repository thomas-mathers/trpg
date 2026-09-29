using Microsoft.Extensions.Options;
using TRPG.Application.Abilities;
using TRPG.Application.Configuration;

namespace TRPG.Application.Combat;

public class CombatTimeScale(IOptions<WorldClockOptions> options)
{
    public static CombatTimeScale Unscaled { get; } = new(Options.Create(new WorldClockOptions()));

    public TimeSpan Round => Scale(CombatTiming.Round);

    public TimeSpan Scale(TimeSpan duration) => duration * options.Value.TimeScale;
}
