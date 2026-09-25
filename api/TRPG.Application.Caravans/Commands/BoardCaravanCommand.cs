using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Queries;
using TRPG.Application.LocationSimulation.Queries;
using TRPG.Application.Routing.Queries;
using TRPG.Data.ModuleContexts;
using TRPG.Domain;
using TRPG.Domain.Models;

namespace TRPG.Application.Caravans.Commands;

public class BoardCaravanCommand
{
    public required Guid PlayerId { get; init; }
    public required Guid CaravanId { get; init; }
    public required Guid PlayerLocationId { get; init; }
    public required TimeSpan Playtime { get; init; }
}

public enum BoardCaravanOutcome
{
    Boarded,
    NoTicket,
    CaravanNotPresent,
    TravelSuspended,
}

public record BoardCaravanResult(
    BoardCaravanOutcome Outcome,
    Guid? DestinationLocationId = null,
    double? TravelTimeHours = null
);

internal class BoardCaravanCommandHandler(
    ICaravansDbContext caravansContext,
    IRoutingDbContext routingContext,
    IQueryHandler<
        ResolveRouteTravelerPositionQuery,
        RouteTimelinePosition?
    > resolveRouteTravelerPosition,
    IQueryHandler<GetWeatherByLocationIdQuery, WeatherCondition?> getWeatherByLocationId
) : ICommandHandler<BoardCaravanCommand, BoardCaravanResult>
{
    public async Task<BoardCaravanResult> Handle(
        BoardCaravanCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var ticket = await caravansContext.CaravanTickets.FirstOrDefaultAsync(
            t => t.CreatureId == command.PlayerId && t.RouteTravelerId == command.CaravanId,
            cancellationToken
        );
        if (ticket == null)
        {
            return new BoardCaravanResult(BoardCaravanOutcome.NoTicket);
        }

        // Once ticketed, the caravan holds for the player as long as they haven't left the stop —
        // re-deriving live position here would let ordinary narration-time overhead (every
        // narrated turn, including the purchase itself, advances playtime a little) make an
        // already-valid ticket look like the caravan already departed.
        if (ticket.OriginStopLocationId != command.PlayerLocationId)
        {
            return new BoardCaravanResult(BoardCaravanOutcome.CaravanNotPresent);
        }

        var positionAtPurchase = await resolveRouteTravelerPosition.Handle(
            new ResolveRouteTravelerPositionQuery
            {
                RouteTravelerId = command.CaravanId,
                Playtime = ticket.PurchasedAtPlaytime,
            },
            cancellationToken
        );
        if (positionAtPurchase is not RouteTimelinePosition.Lingering lingeringAtPurchase)
        {
            return new BoardCaravanResult(BoardCaravanOutcome.CaravanNotPresent);
        }

        var weather = await getWeatherByLocationId.Handle(
            new GetWeatherByLocationIdQuery { LocationId = command.PlayerLocationId },
            cancellationToken
        );
        if (WeatherConditions.PreventsOptionalTravel(weather))
        {
            return new BoardCaravanResult(BoardCaravanOutcome.TravelSuspended);
        }

        var traveler = await routingContext
            .RouteTravelers.AsNoTracking()
            .FirstAsync(t => t.Id == command.CaravanId, cancellationToken);
        var routeSteps = await routingContext
            .RouteSteps.AsNoTracking()
            .Where(step => step.RouteId == traveler.RouteId)
            .OrderBy(step => step.SequenceIndex)
            .ToArrayAsync(cancellationToken);
        var connectorIds = routeSteps
            .Where(step => step.ConnectorId != null)
            .Select(step => step.ConnectorId!.Value)
            .ToArray();

        var distancesByConnectorId = await routingContext
            .TravelConnectors.AsNoTracking()
            .Where(connector => connectorIds.AsEnumerable().Contains(connector.ConnectorId))
            .ToDictionaryAsync(
                connector => connector.ConnectorId,
                connector => (double)connector.Distance,
                cancellationToken
            );
        var steps = routeSteps
            .Select(step => new RouteTimelineStep(
                step.LocationId,
                step.ConnectorId,
                step.ConnectorId == null ? 0 : distancesByConnectorId[step.ConnectorId.Value],
                step.DwellHours
            ))
            .ToArray();

        var fromIndex = steps
            .ToList()
            .FindIndex(step =>
                step.LocationId == ticket.OriginStopLocationId && step.DwellHours > 0
            );
        var toIndex = steps
            .ToList()
            .FindIndex(step =>
                step.LocationId == ticket.DestinationLocationId && step.DwellHours > 0
            );

        // The trip's total duration is fixed the moment the ticket is valid to buy — anchoring to
        // the purchase instant (rather than "now") keeps the destination arrival in sync with the
        // caravan's own schedule no matter how much narration-time overhead accrues between
        // buying the ticket and actually clicking Board.
        var idealTripHours =
            lingeringAtPurchase.HoursUntilDeparture
            + RouteTimeline.HoursBetween(steps, traveler.SpeedUnitsPerHour, fromIndex, toIndex);
        var elapsedHoursSincePurchase =
            (command.Playtime - ticket.PurchasedAtPlaytime) / GameClock.RealTimePerInGameHour;
        var travelTimeHours = Math.Max(0, idealTripHours - elapsedHoursSincePurchase);

        var destinationLocationId = ticket.DestinationLocationId;
        caravansContext.CaravanTickets.Remove(ticket);
        await caravansContext.SaveChangesAsync(cancellationToken);

        return new BoardCaravanResult(
            BoardCaravanOutcome.Boarded,
            destinationLocationId,
            travelTimeHours
        );
    }
}
