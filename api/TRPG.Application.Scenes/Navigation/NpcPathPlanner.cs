using TRPG.Application.Common.Navigation;
using TRPG.Application.Creatures.Results;
using TRPG.Application.Worlds.Queries;
using TRPG.Domain.Models;

namespace TRPG.Application.Scenes.Navigation;

internal sealed class NpcPathPlanner(Func<Point, Point, IReadOnlyList<Point>> route)
{
    public static NpcPathPlanner Straight { get; } = new((from, to) => [from, to]);

    public static NpcPathPlanner ForRoom(NavigationGrid grid) => new(grid.FindPath);

    public static NpcPathPlanner ForDistrict(LocationRoadNetwork network, NavigationGrid offRoad) =>
        new((from, to) => DistrictRoutePlanner.Plan(from, to, network, offRoad));

    public IReadOnlyList<Point> Plan(CreatureResult creature)
    {
        var entry = new Point(creature.EntryX!.Value, creature.EntryY!.Value);
        var anchor = new Point(creature.X, creature.Y);

        return route(entry, anchor);
    }
}
