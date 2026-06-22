using System.Collections.ObjectModel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using TRPG.Data;
using TRPG.Models;

namespace TRPG.Services;

internal class NavigationService(TrpgDbContext context, IMemoryCache cache) {
    public async Task<ReadOnlyCollection<TravelRoute>> GetShortestCityRoute(Guid originCityId, Guid destinationCityId,
        CancellationToken cancellationToken = default) {
        var graph = await cache.GetOrCreateAsync("nav:city-graph", async entry => {
            entry.Priority = CacheItemPriority.NeverRemove;

            var routes = await context.TravelRoutes.ToListAsync(cancellationToken);

            return routes.GroupBy(r => r.OriginCityId).ToDictionary(g => g.Key, g => g.ToList());
        });

        if (graph is null) {
            throw new InvalidOperationException("City graph cache returned null.");
        }

        return Pathfinding.Dijkstra(
            originCityId, destinationCityId,
            id => graph.TryGetValue(id, out var edges) ? edges : [],
            r => r.TravelTime,
            r => r.DestinationCityId);
    }

    public async Task<ReadOnlyCollection<Point>> GetShortestIntraCityRoute(Guid cityId, Point origin, Point destination,
        CancellationToken cancellationToken = default) {
        var grid = await cache.GetOrCreateAsync($"nav:city-grid:{cityId}", async entry => {
            entry.SlidingExpiration = TimeSpan.FromMinutes(10);

            var city = await context.Cities.FindAsync([cityId], cancellationToken)
                       ?? throw new InvalidOperationException($"City {cityId} not found.");

            var buildings = await context.Buildings.Where(b => b.CityId == cityId).ToListAsync(cancellationToken);

            return new GridData(city.Width, city.Height, BuildBlockedFromRectangles(buildings.Select(b => b.Boundary)));
        });

        if (grid is null) {
            throw new InvalidOperationException($"City grid cache returned null for {cityId}.");
        }

        return Pathfinding.Dijkstra(
            origin, destination,
            p => GridNeighbors(p, grid.Width, grid.Height, grid.Blocked),
            _ => 1f,
            p => p);
    }

    public async Task<ReadOnlyCollection<Point>> GetShortestIntraBuildingRoute(Guid buildingId, Point origin,
        Point destination, CancellationToken cancellationToken = default) {
        var grid = await cache.GetOrCreateAsync($"nav:building-grid:{buildingId}", async entry => {
            entry.SlidingExpiration = TimeSpan.FromMinutes(10);

            var building = await context.Buildings.FindAsync([buildingId], cancellationToken)
                           ?? throw new InvalidOperationException($"Building {buildingId} not found.");

            var buildingProps = await context.BuildingProps.Where(p => p.BuildingId == buildingId)
                .ToListAsync(cancellationToken);

            var blockedCells = new HashSet<Point>(buildingProps.Select(p => p.Coordinates));

            var width = building.Boundary.Right - building.Boundary.Left;
            var height = building.Boundary.Bottom - building.Boundary.Top;

            return new GridData(width, height, blockedCells);
        });

        if (grid is null) {
            throw new InvalidOperationException($"Building grid cache returned null for {buildingId}.");
        }

        return Pathfinding.Dijkstra(
            origin, destination,
            p => GridNeighbors(p, grid.Width, grid.Height, grid.Blocked),
            _ => 1f,
            p => p);
    }

    private static IEnumerable<Point> GridNeighbors(Point p, int width, int height, HashSet<Point> blocked) {
        if (p.X > 0 && !blocked.Contains(new Point(p.X - 1, p.Y))) {
            yield return new Point(p.X - 1, p.Y);
        }

        if (p.X < width - 1 && !blocked.Contains(new Point(p.X + 1, p.Y))) {
            yield return new Point(p.X + 1, p.Y);
        }

        if (p.Y > 0 && !blocked.Contains(new Point(p.X, p.Y - 1))) {
            yield return new Point(p.X, p.Y - 1);
        }

        if (p.Y < height - 1 && !blocked.Contains(new Point(p.X, p.Y + 1))) {
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

    private sealed record GridData(int Width, int Height, HashSet<Point> Blocked);
}