using Microsoft.Extensions.Options;

namespace TRPG.Balance;

internal sealed class FixedOptionsSnapshot<T>(T value) : IOptionsSnapshot<T>
    where T : class
{
    public T Value => value;

    public T Get(string? name) => value;
}
