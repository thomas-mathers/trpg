using Microsoft.Extensions.Options;

namespace TRPG.Tests.Helpers;

internal sealed class TestOptionsSnapshot<T>(T value) : IOptionsSnapshot<T>
    where T : class
{
    public T Value => value;

    public T Get(string? name) => value;
}

internal sealed class DefaultOptionsSnapshot<T> : IOptionsSnapshot<T>
    where T : class, new()
{
    public T Value { get; } = new T();

    public T Get(string? name) => Value;
}
