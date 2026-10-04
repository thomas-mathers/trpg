using TRPG.Application.Scenes.Results;
using TRPG.Domain.Models;

namespace TRPG.Application.Scenes.Roads;

public static class SceneRoadResolver
{
    private const double EntryDistance = 1.5;

    public static IReadOnlyList<DistrictRoad> Resolve(
        Footprint size,
        IReadOnlyCollection<SceneNearbyBuildingInfo> buildings,
        IReadOnlyCollection<SceneExitInfo> exits
    )
    {
        var roadBuildings = buildings
            .Select(building => new RoadBuilding(building.Placement, building.Footprint))
            .ToArray();
        var terminals = exits
            .Where(exit => LeadsOutdoors(exit.Destination))
            .Select(exit => ToTerminal(exit.Placement))
            .ToArray();

        return DistrictRoadPlanner.Plan(size, roadBuildings, terminals);
    }

    private static bool LeadsOutdoors(SceneExitDestination destination) =>
        destination
            is SceneBuildingExitDestination
                or SceneDistrictExitDestination
                or SceneWildernessExitDestination;

    private static RoadTerminal ToTerminal(Placement placement)
    {
        var (sin, cos) = Math.SinCos(placement.Angle);

        return new RoadTerminal(
            new Point(placement.X, placement.Y),
            new Point(placement.X + EntryDistance * sin, placement.Y - EntryDistance * cos)
        );
    }
}
