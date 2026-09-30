using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Application.Worlds.Queries;
using TRPG.Data.ModuleContexts;
using TRPG.Domain;

namespace TRPG.Application.Routing.Queries;

public record RouteJourneyQuote(
    Guid RouteId,
    Guid OriginLocationId,
    Guid DestinationLocationId,
    GameInstant DepartureGameTime,
    GameInstant ArrivalGameTime
);

public class GetRouteJourneyQuoteQuery
{
    public required Guid RouteTravelerId { get; init; }
    public required Guid OriginLocationId { get; init; }
    public required Guid DestinationLocationId { get; init; }
    public required GameInstant GameTime { get; init; }
}

internal class GetRouteJourneyQuoteQueryHandler(
    IRoutingDbContext context,
    IQueryHandler<ResolveRouteTravelerPositionQuery, RouteTimelinePosition?> resolvePosition,
    IQueryHandler<
        GetTravelConnectorDistancesQuery,
        IReadOnlyDictionary<Guid, float>
    > getTravelConnectorDistances
) : IQueryHandler<GetRouteJourneyQuoteQuery, RouteJourneyQuote?>
{
    public async Task<RouteJourneyQuote?> Handle(
        GetRouteJourneyQuoteQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var traveler = await LoadTraveler(query.RouteTravelerId, cancellationToken);
        if (traveler == null)
        {
            return null;
        }

        var steps = await LoadSteps(traveler.RouteId, cancellationToken);
        var fromIndex = FindStop(steps, query.OriginLocationId);
        var toIndex = FindStop(steps, query.DestinationLocationId);
        if (fromIndex < 0 || toIndex < 0 || fromIndex == toIndex)
        {
            return null;
        }

        var lingering = await ResolveOriginLinger(query, cancellationToken);
        if (lingering == null)
        {
            return null;
        }

        return CreateQuote(query, traveler, steps, fromIndex, toIndex, lingering);
    }

    private async Task<RouteTravelerQuoteSource?> LoadTraveler(
        Guid routeTravelerId,
        CancellationToken cancellationToken
    ) =>
        await context
            .RouteTravelers.AsNoTracking()
            .Where(candidate => candidate.Id == routeTravelerId)
            .Select(candidate => new RouteTravelerQuoteSource(
                candidate.RouteId,
                candidate.SpeedUnitsPerHour
            ))
            .SingleOrDefaultAsync(cancellationToken);

    private async Task<RouteTimelinePosition.Lingering?> ResolveOriginLinger(
        GetRouteJourneyQuoteQuery query,
        CancellationToken cancellationToken
    )
    {
        var position = await resolvePosition.Handle(
            new ResolveRouteTravelerPositionQuery
            {
                RouteTravelerId = query.RouteTravelerId,
                GameTime = query.GameTime,
            },
            cancellationToken
        );
        return
            position is RouteTimelinePosition.Lingering lingering
            && lingering.LocationId == query.OriginLocationId
            ? lingering
            : null;
    }

    private static RouteJourneyQuote CreateQuote(
        GetRouteJourneyQuoteQuery query,
        RouteTravelerQuoteSource traveler,
        IReadOnlyList<RouteTimelineStep> steps,
        int fromIndex,
        int toIndex,
        RouteTimelinePosition.Lingering lingering
    )
    {
        var departure = query.GameTime + TimeSpan.FromHours(1) * lingering.HoursUntilDeparture;
        var travelHours = RouteTimeline.HoursBetween(
            steps,
            traveler.SpeedUnitsPerHour,
            fromIndex,
            toIndex
        );
        return new RouteJourneyQuote(
            traveler.RouteId,
            query.OriginLocationId,
            query.DestinationLocationId,
            departure,
            departure + TimeSpan.FromHours(1) * travelHours
        );
    }

    private async Task<IReadOnlyList<RouteTimelineStep>> LoadSteps(
        Guid routeId,
        CancellationToken cancellationToken
    )
    {
        var routeSteps = await context
            .RouteSteps.AsNoTracking()
            .Where(step => step.RouteId == routeId)
            .OrderBy(step => step.SequenceIndex)
            .ToArrayAsync(cancellationToken);
        var connectorIds = routeSteps
            .Where(step => step.ConnectorId != null)
            .Select(step => step.ConnectorId!.Value)
            .ToArray();
        var distances = await getTravelConnectorDistances.Handle(
            new GetTravelConnectorDistancesQuery { ConnectorIds = connectorIds },
            cancellationToken
        );
        return routeSteps
            .Select(step => new RouteTimelineStep(
                step.LocationId,
                step.ConnectorId,
                step.ConnectorId == null ? 0 : distances[step.ConnectorId.Value],
                step.DwellHours
            ))
            .ToArray();
    }

    private static int FindStop(IReadOnlyList<RouteTimelineStep> steps, Guid locationId) =>
        steps.ToList().FindIndex(step => step.LocationId == locationId && step.DwellHours > 0);

    private record RouteTravelerQuoteSource(Guid RouteId, double SpeedUnitsPerHour);
}
