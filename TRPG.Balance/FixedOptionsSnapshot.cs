using Microsoft.Extensions.Options;

namespace TRPG.Balance;

// A minimal IOptionsSnapshot<T> that just returns a fixed value, so simulation code can
// construct CombatEngine/DamageCalculator/etc. without going through the DI container.
internal sealed class FixedOptionsSnapshot<T>(T value) : IOptionsSnapshot<T>
    where T : class
{
    public T Value => value;

    public T Get(string? name) => value;
}
