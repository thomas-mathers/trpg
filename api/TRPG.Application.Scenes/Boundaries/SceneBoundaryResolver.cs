using TRPG.Application.Scenes.Results;
using TRPG.Domain.Models;

namespace TRPG.Application.Scenes.Boundaries;

public static class SceneBoundaryResolver
{
    public static DistrictBoundary Resolve(Footprint size, IReadOnlyCollection<SceneExitInfo> exits)
    {
        var boundaryExits = exits.Select(ToBoundaryExit).OfType<BoundaryExit>().ToArray();

        return DistrictBoundaryPlanner.Plan(size, boundaryExits);
    }

    private static BoundaryExit? ToBoundaryExit(SceneExitInfo exit) =>
        exit.Destination switch
        {
            SceneDistrictExitDestination => new BoundaryExit(
                exit.ConnectorId,
                BoundaryExitKind.District,
                exit.Placement
            ),
            SceneWildernessExitDestination => new BoundaryExit(
                exit.ConnectorId,
                BoundaryExitKind.Wilderness,
                exit.Placement
            ),
            _ => null,
        };
}
