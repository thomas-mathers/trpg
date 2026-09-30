using TRPG.Application.Common.Queries;
using TRPG.Application.Worlds.Queries;
using TRPG.Domain;

namespace TRPG.Application.Routing.Queries;

public record RouteTravelDurationRequest(
    Guid CreatureId,
    Guid OriginLocationId,
    Guid DestinationLocationId,
    double SpeedUnitsPerHour
);

public class GetRouteTravelDurationsQuery
{
    public required Guid WorldId { get; init; }
    public required IReadOnlyCollection<RouteTravelDurationRequest> Routes { get; init; }
}

internal class GetRouteTravelDurationsQueryHandler(
    IQueryHandler<GetTravelTopologyQuery, IReadOnlyList<TravelTopologyEdge>> getTravelTopology
) : IQueryHandler<GetRouteTravelDurationsQuery, IReadOnlyDictionary<Guid, TimeSpan>>
{
    public async Task<IReadOnlyDictionary<Guid, TimeSpan>> Handle(
        GetRouteTravelDurationsQuery query,
        CancellationToken cancellationToken = default
    )
    {
        if (query.Routes.Count == 0)
        {
            return new Dictionary<Guid, TimeSpan>();
        }

        Validate(query.Routes);

        var topology = await getTravelTopology.Handle(
            new GetTravelTopologyQuery { WorldIds = [query.WorldId] },
            cancellationToken
        );

        return query.Routes.ToDictionary(
            request => request.CreatureId,
            request => ResolveDuration(request, topology)
        );
    }

    private static TimeSpan ResolveDuration(
        RouteTravelDurationRequest request,
        IReadOnlyCollection<TravelTopologyEdge> topology
    )
    {
        if (request.OriginLocationId == request.DestinationLocationId)
        {
            return TimeSpan.Zero;
        }

        var path = RoutePathfinder.FindShortestPath(
            topology,
            request.OriginLocationId,
            request.DestinationLocationId
        );
        if (path.Count == 0)
        {
            throw new InvalidOperationException("No measured route connects the two locations.");
        }

        var distances = topology.ToDictionary(edge => edge.ConnectorId, edge => edge.Distance);
        var hours = path.Sum(leg => distances[leg.ConnectorId]) / request.SpeedUnitsPerHour;
        return TimeSpan.FromHours(1) * hours;
    }

    private static void Validate(IReadOnlyCollection<RouteTravelDurationRequest> requests)
    {
        if (requests.Select(request => request.CreatureId).Distinct().Count() != requests.Count)
        {
            throw new ArgumentException(
                "A creature can only receive one route duration at a time.",
                nameof(requests)
            );
        }
        if (requests.Any(request => request.SpeedUnitsPerHour <= 0))
        {
            throw new InvalidOperationException("A creature must have positive movement speed.");
        }
    }
}
