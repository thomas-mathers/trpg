using TRPG.Application.Common.Navigation;
using TRPG.Application.Configuration;

namespace TRPG.Tests.Helpers;

internal static class WalkPace
{
    public static double TimeScale { get; } = new WorldClockOptions().TimeScale;

    public static double MetersFor(float movementSpeed, double gameHours) =>
        InLocationPace.MetersPerGameHour(movementSpeed, TimeScale) * gameHours;
}
