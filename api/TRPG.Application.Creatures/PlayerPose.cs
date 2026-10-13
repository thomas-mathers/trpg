using TRPG.Application.Common.Navigation;
using TRPG.Domain.Models;

namespace TRPG.Application.Creatures;

public sealed record PlayerPose(
    Guid LocationId,
    Point Position,
    DateTimeOffset ReportedAt,
    MovementInput Input,
    TimeSpan Leftover
)
{
    public static PlayerPose Idle(Creature creature) =>
        new(
            creature.LocationId,
            new Point(creature.X, creature.Y),
            DateTimeOffset.UnixEpoch,
            new MovementInput(0, 0, creature.Angle),
            TimeSpan.Zero
        );
}
