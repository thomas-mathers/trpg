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
}