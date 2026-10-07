using TRPG.Application.Common.Navigation;
using TRPG.Application.Creatures.Results;
using TRPG.Domain.Models;

namespace TRPG.Application.Scenes.Navigation;

internal sealed class NpcPathPlanner(NavigationGrid? grid)
{
    public static NpcPathPlanner Straight { get; } = new(null);

    public IReadOnlyList<Point> Plan(CreatureResult creature)
    {
        var entry = new Point(creature.EntryX!.Value, creature.EntryY!.Value);
        var anchor = new Point(creature.X, creature.Y);

        return grid?.FindPath(entry, anchor) ?? [entry, anchor];
    }
}
