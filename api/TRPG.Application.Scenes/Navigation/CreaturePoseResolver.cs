using TRPG.Application.Common.Navigation;
using TRPG.Application.Creatures.Results;
using TRPG.Domain;
using TRPG.Domain.Models;

namespace TRPG.Application.Scenes.Navigation;

internal static class CreaturePoseResolver
{
    public static bool HasEntryWalk(CreatureResult creature) =>
        creature is { EntryX: not null, EntryY: not null, EnteredAt: not null };

    public static Placement Resolve(
        CreatureResult creature,
        IReadOnlyList<Point> path,
        GameInstant now,
        double timeScale
    )
    {
        var anchor = new Placement(creature.X, creature.Y, creature.Angle);
        var pace = InLocationPace.MetersPerGameSecond(creature.MovementSpeed, timeScale);

        if (!HasEntryWalk(creature) || path.Count == 0 || pace <= 0)
        {
            return anchor;
        }

        return InLocationPose.Resolve(path, creature.EnteredAt!.Value, pace, now, creature.Angle);
    }
}
