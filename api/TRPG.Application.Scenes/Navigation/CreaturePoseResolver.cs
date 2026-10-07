using TRPG.Application.Common.Navigation;
using TRPG.Application.Creatures.Results;
using TRPG.Domain;
using TRPG.Domain.Models;

namespace TRPG.Application.Scenes.Navigation;

internal static class CreaturePoseResolver
{
    public static Placement Resolve(CreatureResult creature, GameInstant now, double timeScale)
    {
        var anchor = new Placement(creature.X, creature.Y, creature.Angle);

        if (
            creature.EntryX is not { } entryX
            || creature.EntryY is not { } entryY
            || creature.EnteredAt is not { } enteredAt
        )
        {
            return anchor;
        }

        var pace = InLocationPace.MetersPerGameSecond(creature.MovementSpeed, timeScale);
        if (pace <= 0)
        {
            return anchor;
        }

        Point[] path = [new(entryX, entryY), new(creature.X, creature.Y)];

        return InLocationPose.Resolve(path, enteredAt, pace, now, creature.Angle);
    }
}
