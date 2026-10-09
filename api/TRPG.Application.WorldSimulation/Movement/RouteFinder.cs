using TRPG.Application.Common.Navigation;

namespace TRPG.Application.WorldSimulation.Movement;

internal sealed class RouteFinder(TravelGraph graph, int searchesPerTick)
{
    private readonly Dictionary<RouteKey, IReadOnlyList<RouteLeg>> _cache = [];
    private int _remainingSearches = searchesPerTick;

    public void BeginTick() => _remainingSearches = searchesPerTick;

    public bool TryFind(
        Guid originLocationId,
        Guid? originNodeId,
        Guid destinationLocationId,
        out IReadOnlyList<RouteLeg> legs
    )
    {
        var key = new RouteKey(originLocationId, originNodeId, destinationLocationId);
        if (_cache.TryGetValue(key, out var cached))
        {
            legs = cached;
            return true;
        }

        if (_remainingSearches == 0)
        {
            legs = [];
            return false;
        }

        _remainingSearches--;
        legs = graph.FindShortestPath(originLocationId, originNodeId, destinationLocationId);
        _cache[key] = legs;
        return true;
    }

    private readonly record struct RouteKey(
        Guid OriginLocationId,
        Guid? OriginNodeId,
        Guid DestinationLocationId
    );
}
