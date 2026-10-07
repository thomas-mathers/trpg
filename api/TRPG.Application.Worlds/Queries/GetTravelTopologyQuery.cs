using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using TRPG.Application.Common.Navigation;
using TRPG.Application.Common.Queries;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Worlds.Queries;

public record TravelTopology(
    IReadOnlyList<LocationConnector> LocationConnectors,
    IReadOnlyList<PointConnector> PointConnectors,
    IReadOnlyList<TravelNode> Nodes
)
{
    private TravelGraph? _graph;

    public TravelGraph ToGraph() =>
        _graph ??= new([.. LocationConnectors, .. PointConnectors], Nodes);
}

public class GetTravelTopologyQuery
{
    public required Guid WorldId { get; init; }
}

internal class GetTravelTopologyQueryHandler(IWorldsDbContext context, IMemoryCache cache)
    : IQueryHandler<GetTravelTopologyQuery, TravelTopology>
{
    public static string CacheKey(Guid worldId) => $"travel-topology:{worldId}";

    public async Task<TravelTopology> Handle(
        GetTravelTopologyQuery query,
        CancellationToken cancellationToken = default
    )
    {
        if (
            cache.TryGetValue(CacheKey(query.WorldId), out TravelTopology? cached)
            && cached is not null
        )
        {
            return cached;
        }

        var topology = await Load(query.WorldId, cancellationToken);

        if (topology.Nodes.Count > 0)
        {
            cache.Set(CacheKey(query.WorldId), topology, TimeSpan.FromHours(1));
        }

        return topology;
    }

    private async Task<TravelTopology> Load(Guid worldId, CancellationToken cancellationToken)
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

        return new TravelTopology(locationConnectors, pointConnectors, nodes);
    }
}
