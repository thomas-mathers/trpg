using TRPG.Application.Scenes.Results;
using TRPG.Domain.Models;

namespace TRPG.Application.Scenes.GreenSpaces;

public static class GreenSpacePlanner
{
    private const double TreePairDistance = 25;
    private const double BenchDistance = 42;

    public static IReadOnlyCollection<SceneGreenSpaceInfo> Plan(
        IReadOnlyCollection<ScenePropInfo> props
    )
    {
        var trees = props.Where(prop => prop.Model == PropModel.FurnitureTree).ToArray();
        var shrubs = props.Where(prop => prop.Model == PropModel.FurnitureShrub).ToArray();
        if (shrubs.Length == 0)
            return [];

        var courtTrees = trees
            .Where(tree => shrubs.Any(shrub => DistanceSquared(tree, shrub) <= 7.5 * 7.5))
            .ToArray();
        var squareTrees = trees.Except(courtTrees).ToArray();
        return courtTrees.Select(CourtSpace).Concat(PairedSpaces(squareTrees, props)).ToArray();
    }

    private static SceneGreenSpaceInfo CourtSpace(ScenePropInfo tree) =>
        new(tree.Id, tree.Placement, new Footprint(10.5, 18));

    private static IEnumerable<SceneGreenSpaceInfo> PairedSpaces(
        ScenePropInfo[] trees,
        IReadOnlyCollection<ScenePropInfo> props
    )
    {
        var remaining = trees.ToList();
        var benches = props.Where(IsBench).ToArray();
        while (remaining.Count > 1)
        {
            var first = remaining[0];
            var closest = remaining.Skip(1).OrderBy(tree => DistanceSquared(first, tree)).First();
            remaining.Remove(first);
            if (DistanceSquared(first, closest) > TreePairDistance * TreePairDistance)
                continue;

            remaining.Remove(closest);
            var points = new[] { first, closest }
                .Concat(
                    benches.Where(bench =>
                        DistanceSquared(first, bench) <= BenchDistance * BenchDistance
                        || DistanceSquared(closest, bench) <= BenchDistance * BenchDistance
                    )
                )
                .ToArray();
            var minX = points.Min(point => point.Placement.X);
            var maxX = points.Max(point => point.Placement.X);
            var minY = points.Min(point => point.Placement.Y);
            var maxY = points.Max(point => point.Placement.Y);
            yield return new SceneGreenSpaceInfo(
                first.Id,
                new Placement((minX + maxX) / 2, (minY + maxY) / 2, 0),
                new Footprint(
                    Math.Max(
                        13,
                        Math.Max(
                            maxX - minX + 3,
                            Math.Abs(first.Placement.X - closest.Placement.X) + 10
                        )
                    ),
                    Math.Max(
                        13,
                        Math.Max(
                            maxY - minY + 3,
                            Math.Abs(first.Placement.Y - closest.Placement.Y) + 10
                        )
                    )
                )
            );
        }
    }

    private static bool IsBench(ScenePropInfo prop) =>
        prop.Model
            is PropModel.FurnitureBench
                or PropModel.SeatBench
                or PropModel.SeatStoneBench
                or PropModel.SeatLowWall;

    private static double DistanceSquared(ScenePropInfo first, ScenePropInfo second) =>
        Math.Pow(first.Placement.X - second.Placement.X, 2)
        + Math.Pow(first.Placement.Y - second.Placement.Y, 2);
}
