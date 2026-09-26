using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data.ModuleContexts;
using TRPG.Domain;
using TRPG.Domain.Models;

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

internal class GetRouteTravelDurationsQueryHandler(IRoutingDbContext context)
    : IQueryHandler<GetRouteTravelDurationsQuery, IReadOnlyDictionary<Guid, TimeSpan>>
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

        var connectors = await context
            .LocationConnectors.AsNoTracking()
            .Where(connector => connector.WorldId == query.WorldId)
            .ToArrayAsync(cancellationToken);

        var travelConnectors = await context
            .TravelConnectors.AsNoTracking()
            .Where(connector => connector.WorldId == query.WorldId)
            .ToArrayAsync(cancellationToken);
        var distanceByConnectorId = travelConnectors.ToDictionary(
            connector => connector.ConnectorId,
            connector => connector.Distance
        );

        return query.Routes.ToDictionary(
            request => request.CreatureId,
            request => ResolveDuration(request, connectors, travelConnectors, distanceByConnectorId)
        );
    }

    private static TimeSpan ResolveDuration(
        RouteTravelDurationRequest request,
        IReadOnlyCollection<LocationConnector> connectors,
        IReadOnlyCollection<TravelConnector> travelConnectors,
        IReadOnlyDictionary<Guid, float> distanceByConnectorId
    )
    {
        if (request.OriginLocationId == request.DestinationLocationId)
        {
            return TimeSpan.Zero;
        }

        var path = RoutePathfinder.FindShortestPath(
            connectors,
            travelConnectors,
            request.OriginLocationId,
            request.DestinationLocationId
        );
        if (path.Count == 0)
        {
            throw new InvalidOperationException("No measured route connects the two locations.");
        }

        var hours =
            path.Sum(leg => distanceByConnectorId[leg.ConnectorId]) / request.SpeedUnitsPerHour;
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
