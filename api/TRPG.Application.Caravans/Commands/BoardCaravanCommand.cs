using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Queries;
using TRPG.Application.Routing.Queries;
using TRPG.Application.Weather.Queries;
using TRPG.Data.ModuleContexts;
using TRPG.Domain;
using TRPG.Domain.Models;

namespace TRPG.Application.Caravans.Commands;

public class BoardCaravanCommand
{
    public required Guid PlayerId { get; init; }
    public required Guid CaravanId { get; init; }
    public required Guid PlayerLocationId { get; init; }
    public required GameInstant GameTime { get; init; }
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
    IQueryHandler<GetRouteJourneyQuoteQuery, RouteJourneyQuote?> getJourneyQuote,
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

        if (ticket.OriginStopLocationId != command.PlayerLocationId)
        {
            return new BoardCaravanResult(BoardCaravanOutcome.CaravanNotPresent);
        }

        var quote = await getJourneyQuote.Handle(
            new GetRouteJourneyQuoteQuery
            {
                RouteTravelerId = command.CaravanId,
                OriginLocationId = ticket.OriginStopLocationId,
                DestinationLocationId = ticket.DestinationLocationId,
                GameTime = ticket.PurchasedAtGameTime,
            },
            cancellationToken
        );
        if (quote == null)
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

        var travelTimeHours = Math.Max(
            0,
            (quote.ArrivalGameTime - command.GameTime) / TimeSpan.FromHours(1)
        );

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
