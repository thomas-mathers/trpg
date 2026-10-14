using Microsoft.EntityFrameworkCore;
using TRPG.Application.WorldGeneration.Generators;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.WorldSimulation.Fights;

internal sealed class ChasePathPlanner(IPropsDbContext props, IWorldsDbContext worlds)
{
    public async Task<IReadOnlyList<Point>?> Plan(
        Guid locationId,
        Point from,
        Point target,
        double stopDistance,
        CancellationToken cancellationToken
    )
    {
        var location = await worlds
            .Locations.AsNoTracking()
            .SingleAsync(location => location.Id == locationId, cancellationToken);
        var localProps = await props
            .Props.AsNoTracking()
            .Where(prop => prop.LocationId == locationId)
            .ToArrayAsync(cancellationToken);
        var buildings = await worlds
            .Buildings.AsNoTracking()
            .Where(building => building.ExteriorLocationId == locationId)
            .ToArrayAsync(cancellationToken);
        var grid = LocationNavigationGrid.Build(location, localProps, buildings);

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
