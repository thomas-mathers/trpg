using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using TRPG.Algorithms;
using TRPG.Data;
using TRPG.Models;

namespace TRPG.Services;

internal class NavigationService(TrpgDbContext context, IMemoryCache cache) {
    public async Task<IReadOnlyCollection<Road>> GetShortestStateRoute(Guid originStateId, Guid destinationStateId,
        CancellationToken cancellationToken = default) {
        var graph = await cache.GetOrCreateAsync("nav:state-graph", async entry => {
            entry.Priority = CacheItemPriority.NeverRemove;

            var roads = await context.Roads.ToListAsync(cancellationToken);

            return roads.GroupBy(r => r.OriginStateId).ToDictionary(g => g.Key, g => g.ToList());
        });

        if (graph is null) {
            throw new InvalidOperationException("State graph cache returned null.");
        }

        var statePath = Graphs.ShortestPath(
            originStateId, destinationStateId,
            id => graph.TryGetValue(id, out var rs) ? rs.Select(r => r.DestinationStateId) : [],
            (from, to) => graph[from].First(r => r.DestinationStateId == to).TravelTime);

        return statePath
            .Zip(statePath.Skip(1), (from, to) => graph[from].First(r => r.DestinationStateId == to))
            .ToArray();
    }
}