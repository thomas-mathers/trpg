using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Game;
using TRPG.Application.Game.Commands;
using TRPG.Application.Worlds.Queries;
using TRPG.Contracts;
using TRPG.Requests;
using TRPG.Responses;

namespace TRPG.Endpoints;

internal static class SessionEndpoints
{
    public static void MapSessionEndpoints(this WebApplication app)
    {
        app.MapPost("/worlds/{worldId:guid}/sessions", StartSession);
        app.MapPost("/sessions/{sessionId:guid}/chat", SendChat);
        app.MapPost("/sessions/{sessionId:guid}/wait", Wait);
        app.MapDelete("/sessions/{sessionId:guid}", EndSession);
    }

    private static async Task<IResult> StartSession(
        Guid worldId,
        GetWorldQueryHandler getWorld,
        CreateGameSessionCommandHandler createGameSession,
        CancellationToken cancellationToken
    )
    {
        var world = await getWorld.Handle(
            new GetWorldQuery { WorldId = worldId },
            cancellationToken
        );
        if (world?.PlayerId == null)
        {
            return Results.NotFound();
        }

        var sessionId = await createGameSession.Handle(
            new CreateGameSessionCommand
            {
                WorldId = worldId,
                PlayerId = world.PlayerId.Value,
                Playtime = world.Playtime,
            },
            cancellationToken
        );

        return Results.Ok(new CreateSessionResponse(sessionId));
    }

    private static async Task<IResult> SendChat(
        Guid sessionId,
        ChatRequest request,
        bool? includeMetrics,
        HttpContext httpContext,
        CancellationToken cancellationToken
    )
    {
        httpContext.RequestServices.GetRequiredService<GameTurnContext>().SessionId = sessionId;
        var turnRunner = httpContext.RequestServices.GetRequiredService<GameTurnRunner>();

        var metrics = await turnRunner.GetResponseWithMetrics(request.Message, cancellationToken);
        return Results.Ok(
            new ChatResponse(metrics.Response, includeMetrics == true ? ToDto(metrics) : null)
        );
    }

    private static async Task<IResult> Wait(
        Guid sessionId,
        WaitRequest request,
        GameSessionLocks sessionLocks,
        AdvanceTimeCommandHandler advanceTime,
        CancellationToken cancellationToken
    )
    {
        if (request.Hours <= 0)
        {
            return Results.BadRequest();
        }

        await using var @lock = await sessionLocks.Acquire(sessionId, cancellationToken);
        var bankedPlaytime = await advanceTime.Handle(
            new AdvanceTimeCommand
            {
                Lock = @lock,
                Delta = request.Hours * GameClock.RealTimePerInGameHour,
            },
            cancellationToken
        );

        var currentDate = GameClock.GetCurrentInGameDate(bankedPlaytime);
        var message =
            $"Time passes... it is now {currentDate.WeekdayName}, hour {currentDate.Hour}.";
        return Results.Ok(new WaitResponse(message));
    }

    private static async Task<IResult> EndSession(
        Guid sessionId,
        EndGameSessionCommandHandler endGameSession,
        CancellationToken cancellationToken
    )
    {
        await endGameSession.Handle(
            new EndGameSessionCommand { SessionId = sessionId },
            cancellationToken
        );
        return Results.NoContent();
    }

    private static TurnMetricsDto ToDto(TurnMetrics metrics) =>
        new(metrics.FirstTokenMs, metrics.TotalMs, metrics.TokenCount, metrics.TokensPerSecond);
}
