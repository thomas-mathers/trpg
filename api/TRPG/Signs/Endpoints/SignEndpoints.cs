using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using TRPG.Application.Caravans.Queries;
using TRPG.Application.Common.Exceptions;
using TRPG.Application.Common.Queries;
using TRPG.Application.GameSessions.Queries;
using TRPG.Application.Props.Queries;
using TRPG.Domain;
using TRPG.Domain.Models;
using TRPG.Signs.Responses;

namespace TRPG.Signs.Endpoints;

// Composes across Props (generic prop lookup) and Caravans (live arrival math) — neither module
// depends on the other, so this dispatch lives at the host, the same way the world map composes
// across modules that don't depend on each other either.
internal static class SignEndpoints
{
    public static void MapSignEndpoints(this WebApplication app)
    {
        app.MapGet("/signs/{signId:guid}", GetSignText)
            .WithName("GetSignText")
            .ProducesProblem(StatusCodes.Status404NotFound);
    }

    private static async Task<Ok<SignTextResponse>> GetSignText(
        Guid signId,
        Guid worldId,
        [FromServices] IQueryHandler<GetPropByIdQuery, Prop?> getPropById,
        [FromServices] IQueryHandler<GetPlaytimeByWorldIdQuery, TimeSpan> getPlaytimeByWorldId,
        [FromServices]
            IQueryHandler<
            GetNextCaravanArrivalsQuery,
            IReadOnlyList<NextCaravanArrival>
        > getNextCaravanArrivals,
        CancellationToken cancellationToken
    )
    {
        var prop = await getPropById.Handle(
            new GetPropByIdQuery { Id = signId },
            cancellationToken
        );
        if (prop is not Sign sign)
        {
            throw new EntityNotFoundException("Sign", signId);
        }

        if (sign is not CaravanScheduleSign)
        {
            return TypedResults.Ok(new SignTextResponse(sign.Description));
        }

        var playtime = await getPlaytimeByWorldId.Handle(
            new GetPlaytimeByWorldIdQuery { WorldId = worldId },
            cancellationToken
        );
        var arrivals = await getNextCaravanArrivals.Handle(
            new GetNextCaravanArrivalsQuery
            {
                WorldId = worldId,
                LocationId = sign.LocationId,
                Playtime = playtime,
            },
            cancellationToken
        );

        var lines = arrivals
            .OrderBy(arrival => arrival.Direction)
            .Select(arrival => FormatArrival(arrival, playtime));
        return TypedResults.Ok(
            new SignTextResponse("Caravan schedule:\n" + string.Join("\n", lines))
        );
    }

    private static string FormatArrival(NextCaravanArrival arrival, TimeSpan currentPlaytime)
    {
        var directionLabel =
            arrival.Direction == RouteDirection.Clockwise ? "Clockwise" : "Counter-clockwise";
        if (arrival.HoursUntilArrival <= 0)
        {
            return $"{directionLabel}: here now";
        }

        var arrivalPlaytime =
            currentPlaytime + GameClock.RealTimePerInGameHour * arrival.HoursUntilArrival;
        var arrivalDate = GameClock.GetCurrentInGameDate(arrivalPlaytime);

        return $"{directionLabel}: next arrival {arrivalDate.WeekdayName}, {arrivalDate.MonthName} {arrivalDate.Day} - {arrivalDate.Hour}:00";
    }
}
