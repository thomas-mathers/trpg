using Microsoft.Extensions.Options;
using TRPG.Application.Common.Navigation;
using TRPG.Application.Common.Queries;
using TRPG.Application.Configuration;
using TRPG.Application.Worlds.Queries;
using TRPG.Domain;

namespace TRPG.Application.Routing.Queries;

public record RouteTravelDurationRequest(
    Guid CreatureId,
    Guid OriginLocationId,
    Guid DestinationLocationId,
    float MovementSpeed
);

public class GetRouteTravelDurationsQuery
{
    public required Guid WorldId { get; init; }
    public required IReadOnlyCollection<RouteTravelDurationRequest> Routes { get; init; }
}

internal class GetRouteTravelDurationsQueryHandler(
    IQueryHandler<GetTravelTopologyQuery, TravelTopology> getTravelTopology,
    IOptions<WorldClockOptions> clockOptions
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
            new GetTravelTopologyQuery { WorldId = query.WorldId },
            cancellationToken
        );
        var graph = topology.ToGraph();

        return query.Routes.ToDictionary(
            request => request.CreatureId,
            request => ResolveDuration(request, graph, clockOptions.Value.TimeScale)
        );
    }

    private static TimeSpan ResolveDuration(
        RouteTravelDurationRequest request,
        TravelGraph graph,
        double timeScale
    )
    {
        if (request.OriginLocationId == request.DestinationLocationId)
        {
            return TimeSpan.Zero;
        }

        var path = graph.FindShortestPath(
            request.OriginLocationId,
            null,
            request.DestinationLocationId
        );
        if (path.Count == 0)
        {
            throw new InvalidOperationException("No measured route connects the two locations.");
        }

        var hours =
            path.Sum(leg => leg.Distance)
            / InLocationPace.MetersPerGameHour(request.MovementSpeed, timeScale);
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
        if (requests.Any(request => request.MovementSpeed <= 0))
        {
            throw new InvalidOperationException("A creature must have positive movement speed.");
        }
    }
}
