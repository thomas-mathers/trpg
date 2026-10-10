using TRPG.Application.Common.Navigation;
using TRPG.Domain.Models;

namespace TRPG.Application.Routing;

public static class JourneyPace
{
    public static double MetersPerGameSecond(
        IReadOnlyCollection<Creature> members,
        double timeScale
    )
    {
        var slowest = members.Min(member => member.MovementSpeed);

        return InLocationPace.MetersPerGameSecond(slowest, timeScale);
    }
}
