using TRPG.Application.Abilities;
using TRPG.Domain;

namespace TRPG.Tests.Helpers;

internal static class TestTime
{
    public static GameInstant Start { get; } =
        new(new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Unspecified));

    public static GameInstant AfterRounds(int rounds) => Start + CombatTiming.Rounds(rounds);
}
