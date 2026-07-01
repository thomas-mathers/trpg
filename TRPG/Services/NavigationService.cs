using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using TRPG.Algorithms;
using TRPG.Data;
using TRPG.Models;

namespace TRPG.Services;

internal class NavigationService(TrpgDbContext context, IMemoryCache cache) {
    public async Task<IReadOnlyCollection<Road>> GetShortestRegionRoute(Guid originRegionId, Guid destinationRegionId,
        CancellationToken cancellationToken = default) {
        var graph = await cache.GetOrCreateAsync("nav:region-graph", async entry => {
            entry.Priority = CacheItemPriority.NeverRemove;

            var roads = await context.Roads.ToListAsync(cancellationToken);

            return roads.GroupBy(r => r.OriginRegionId).ToDictionary(g => g.Key, g => g.ToList());
        });

        if (graph is null) {
            throw new InvalidOperationException("Region graph cache returned null.");
        }

        var regionPath = Graphs.ShortestPath(
            originRegionId, destinationRegionId,
            id => graph.TryGetValue(id, out var rs) ? rs.Select(r => r.DestinationRegionId) : [],
            (from, to) => graph[from].First(r => r.DestinationRegionId == to).TravelTime);

        return regionPath
            .Zip(regionPath.Skip(1), (from, to) => graph[from].First(r => r.DestinationRegionId == to))
            .ToArray();
    }

    public async Task<IReadOnlyCollection<Point>> GetShortestIntraRegionRoute(Guid regionId, Point origin,
        Point destination,
        CancellationToken cancellationToken = default) {
        var grid = await cache.GetOrCreateAsync($"nav:region-grid:{regionId}", async entry => {
            entry.SlidingExpiration = TimeSpan.FromMinutes(10);

            var region = await context.Regions.FindAsync([regionId], cancellationToken)
                         ?? throw new InvalidOperationException($"Region {regionId} not found.");

            var buildings = await context.Buildings.Where(b => b.RegionId == regionId).ToListAsync(cancellationToken);

            return new GridData(region.Width, region.Height, BuildBlockedFromRectangles(buildings.Select(b => b.Boundary)));
        });

        if (grid is null) {
            throw new InvalidOperationException($"Region grid cache returned null for {regionId}.");
        }

        return Graphs.ShortestPath(
            origin, destination,
            p => GridNeighbors(p, grid),
            (_, _) => 1f);
    }

    private static IEnumerable<Point> GridNeighbors(Point p, GridData grid) {
        if (p.X > 0 && !grid.Blocked.Contains(new Point(p.X - 1, p.Y))) {
            yield return new Point(p.X - 1, p.Y);
        }

        if (p.X < grid.Width - 1 && !grid.Blocked.Contains(new Point(p.X + 1, p.Y))) {
            yield return new Point(p.X + 1, p.Y);
        }

        if (p.Y > 0 && !grid.Blocked.Contains(new Point(p.X, p.Y - 1))) {
            yield return new Point(p.X, p.Y - 1);
        }

        if (p.Y < grid.Height - 1 && !grid.Blocked.Contains(new Point(p.X, p.Y + 1))) {
            yield return new Point(p.X, p.Y + 1);
        }
    }

    private static HashSet<Point> BuildBlockedFromRectangles(IEnumerable<Rectangle> rectangles) {
        var blocked = new HashSet<Point>();
        foreach (var r in rectangles) {
            for (var x = r.Left; x < r.Right; x++) {
                for (var y = r.Top; y < r.Bottom; y++) {
                    blocked.Add(new Point(x, y));
                }
            }
        }

        return blocked;
    }

    private record GridData(int Width, int Height, HashSet<Point> Blocked);
}