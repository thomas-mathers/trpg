using TRPG.Application.WorldSimulation.LocalActivities;
using TRPG.Domain.Models;

namespace TRPG.Application.WorldSimulation.Fights;

internal sealed class ChasePathPlanner(LocationNavigationGrids grids)
{
    public async Task<IReadOnlyList<Point>?> Plan(
        Guid locationId,
        Point from,
        Point target,
        double stopDistance,
        CancellationToken cancellationToken
    )
    {
        var grid = await grids.Get(locationId, cancellationToken);

        return StopShort(grid.FindPath(from, target), stopDistance);
    }

    private static IReadOnlyList<Point>? StopShort(IReadOnlyList<Point> path, double stopDistance)
    {
        var keep =
            path.Zip(path.Skip(1)).Sum(pair => Distance(pair.First, pair.Second)) - stopDistance;
        if (keep <= 0)
        {
            return null;
        }

        var shortened = new List<Point> { path[0] };
        foreach (var pair in path.Zip(path.Skip(1)))
        {
            var length = Distance(pair.First, pair.Second);
            if (length >= keep)
            {
                var fraction = keep / length;
                shortened.Add(
                    new Point(
                        pair.First.X + ((pair.Second.X - pair.First.X) * fraction),
                        pair.First.Y + ((pair.Second.Y - pair.First.Y) * fraction)
                    )
                );
                return shortened;
            }

            shortened.Add(pair.Second);
            keep -= length;
        }

        return shortened;
    }

    private static double Distance(Point first, Point second) =>
        Math.Sqrt(Math.Pow(second.X - first.X, 2) + Math.Pow(second.Y - first.Y, 2));
}
