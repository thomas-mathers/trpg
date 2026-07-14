using Microsoft.Extensions.Options;

namespace TRPG.Tests.Helpers;

internal sealed class TestOptionsSnapshot<T>(T value) : IOptionsSnapshot<T>
    where T : class
{
    public T Value => value;

    public T Get(string? name) => value;
}
