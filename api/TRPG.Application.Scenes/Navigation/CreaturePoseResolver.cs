using TRPG.Application.Common.Navigation;
using TRPG.Application.Creatures.Results;
using TRPG.Domain;
using TRPG.Domain.Models;

namespace TRPG.Application.Scenes.Navigation;

internal static class CreaturePoseResolver
{
    public static bool HasEntryWalk(CreatureResult creature) =>
        creature is { EntryX: not null, EntryY: not null, EnteredAt: not null };

    public static bool HasExitWalk(CreatureResult creature) =>
        creature is { ExitX: not null, ExitY: not null, DepartedAt: not null };

    public static bool HasWalk(CreatureResult creature) =>
        HasEntryWalk(creature) || HasExitWalk(creature);

    public static Placement Resolve(
        CreatureResult creature,
        IReadOnlyList<Point> path,
        GameInstant now,
        double timeScale
    )
    {
        var pace = InLocationPace.MetersPerGameSecond(creature.MovementSpeed, timeScale);

        if (!HasWalk(creature) || path.Count == 0 || pace <= 0)
        {
            return new Placement(creature.X, creature.Y, creature.Angle);
        }

        return InLocationPose.Resolve(path, StartedAt(creature), pace, now, creature.Angle);
    }

    public static bool HasLeft(
        CreatureResult creature,
        IReadOnlyList<Point> path,
        GameInstant now,
        double timeScale
    )
    {
        var pace = InLocationPace.MetersPerGameSecond(creature.MovementSpeed, timeScale);

        return HasExitWalk(creature)
            && path.Count > 0
            && InLocationPose.HasFinished(path, StartedAt(creature), pace, now);
    }

    private static GameInstant StartedAt(CreatureResult creature) =>
        HasExitWalk(creature) ? creature.DepartedAt!.Value : creature.EnteredAt!.Value;
}
