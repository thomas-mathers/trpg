using TRPG.Application.Scenes.Neighbors;
using TRPG.Application.Scenes.Results;
using TRPG.Domain.Models;

namespace TRPG.Application.Scenes.Boundaries;

public static class SceneBoundaryResolver
{
    public static DistrictBoundary Resolve(
        Footprint size,
        IReadOnlyCollection<SceneExitInfo> exits,
        IReadOnlyCollection<NeighborDistrict> neighbors
    )
    {
        var pockets = DistrictCornerPlanner.Find(size, neighbors);
        var boundaryExits = exits
            .Select(exit => ToBoundaryExit(size, exit, neighbors, pockets))
            .OfType<BoundaryExit>()
            .ToArray();

        return DistrictBoundaryPlanner.Plan(size, boundaryExits);
    }

    private static BoundaryExit? ToBoundaryExit(
        Footprint size,
        SceneExitInfo exit,
        IReadOnlyCollection<NeighborDistrict> neighbors,
        IReadOnlyCollection<CornerPocket> pockets
    ) =>
        exit.Destination switch
        {
            SceneDistrictExitDestination => new BoundaryExit(
                exit.ConnectorId,
                BoundaryExitKind.District,
                exit.Placement,
                WithPockets(SharedOpening(size, exit, neighbors), exit, pockets)
            ),
            SceneWildernessExitDestination => new BoundaryExit(
                exit.ConnectorId,
                BoundaryExitKind.Wilderness,
                exit.Placement
            ),
            _ => null,
        };

    private static EdgeSpan? WithPockets(
        EdgeSpan? opening,
        SceneExitInfo exit,
        IReadOnlyCollection<CornerPocket> pockets
    ) =>
        pockets
            .Where(pocket => pocket.InsetId == exit.DestinationLocationId)
            .Aggregate(
                opening,
                (span, pocket) =>
                    span == null
                        ? null
                        : new EdgeSpan(
                            Math.Min(span.Start, pocket.InsetGap.Start),
                            Math.Max(span.End, pocket.InsetGap.End)
                        )
            );

    private static EdgeSpan? SharedOpening(
        Footprint size,
        SceneExitInfo exit,
        IReadOnlyCollection<NeighborDistrict> neighbors
    )
    {
        var edge = DistrictBoundaryPlanner.EdgeOf(size, exit.Placement);
        var neighbor = neighbors.FirstOrDefault(candidate =>
            candidate.LocationId == exit.DestinationLocationId
        );

        return edge == null || neighbor == null
            ? null
            : NeighborPreviewPlanner.SharedSpan(size, edge.Value, neighbor.Origin, neighbor.Size);
    }
}
