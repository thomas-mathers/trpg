using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using TRPG.Application.Common.Navigation;
using TRPG.Application.Common.Queries;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Worlds.Queries;

public class GetTravelGraphQuery
{
    public required Guid WorldId { get; init; }
}

internal class GetTravelGraphQueryHandler(IWorldsDbContext context, IMemoryCache cache)
    : IQueryHandler<GetTravelGraphQuery, TravelGraph>
{
    public static string CacheKey(Guid worldId) => $"travel-graph:{worldId}";

    public async Task<TravelGraph> Handle(
        GetTravelGraphQuery query,
        CancellationToken cancellationToken = default
    )
    {
        if (
            cache.TryGetValue(CacheKey(query.WorldId), out TravelGraph? cached)
            && cached is not null
        )
        {
            return cached;
        }

        var graph = await Load(query.WorldId, cancellationToken);

        if (graph.HasNodes)
        {
            cache.Set(CacheKey(query.WorldId), graph, TimeSpan.FromHours(1));
        }

        return graph;
    }

    private async Task<TravelGraph> Load(Guid worldId, CancellationToken cancellationToken)
    {
        var locationConnectors = await context
            .LocationConnectors.AsNoTracking()
            .Where(connector => connector.WorldId == worldId)
            .ToArrayAsync(cancellationToken);
        var pointConnectors = await context
            .PointConnectors.AsNoTracking()
            .Where(connector => connector.WorldId == worldId)
            .ToArrayAsync(cancellationToken);
        var nodes = await context
            .TravelNodes.AsNoTracking()
            .Where(node => node.WorldId == worldId)
            .ToArrayAsync(cancellationToken);

        return new TravelGraph([.. locationConnectors, .. pointConnectors], nodes);
    }
}
