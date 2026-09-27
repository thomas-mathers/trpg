using TRPG.Application.Creatures;

namespace TRPG.Tests.Helpers;

internal sealed class TestChanceRoller : IChanceRoller
{
    public Queue<bool> Results { get; } = new();
    public bool Result { get; set; } = true;

    public bool Roll(float chance) => Results.TryDequeue(out var result) ? result : Result;
}
