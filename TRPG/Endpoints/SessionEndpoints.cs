using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Game;
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
        GameSessionStateStore sessionStore,
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

        var session = new GameSession(worldId, world.PlayerId.Value, world.Playtime);
        var messages = new List<ChatMessage> { new(ChatRole.System, GameTurnRunner.SystemPrompt) };
        var state = new GameSessionState(session, messages);
        var sessionId = sessionStore.Add(state);

        return Results.Ok(new CreateSessionResponse(sessionId));
    }

    private static async Task<IResult> SendChat(
        Guid sessionId,
        ChatRequest request,
        bool? includeMetrics,
        GameSessionStateStore sessionStore,
        HttpContext httpContext,
        CancellationToken cancellationToken
    )
    {
        var state = sessionStore.Get(sessionId);
        if (state == null)
        {
            return Results.NotFound();
        }

        httpContext.RequestServices.GetRequiredService<CurrentGameSessionStateAccessor>().State =
            state;
        var turnRunner = httpContext.RequestServices.GetRequiredService<GameTurnRunner>();
        var metrics = await turnRunner.ProcessTurn(request.Message, cancellationToken);

        return Results.Ok(
            new TRPG.Responses.ChatResponse(
                metrics.Response,
                includeMetrics == true ? ToDto(metrics) : null
            )
        );
    }

    private static async Task<IResult> Wait(
        Guid sessionId,
        WaitRequest request,
        GameSessionStateStore sessionStore,
        HttpContext httpContext,
        CancellationToken cancellationToken
    )
    {
        var state = sessionStore.Get(sessionId);
        if (state == null)
        {
            return Results.NotFound();
        }

        if (request.Hours <= 0)
        {
            return Results.BadRequest();
        }

        httpContext.RequestServices.GetRequiredService<CurrentGameSessionStateAccessor>().State =
            state;
        var turnRunner = httpContext.RequestServices.GetRequiredService<GameTurnRunner>();
        var currentDate = await turnRunner.AdvanceTime(request.Hours, cancellationToken);
        var message =
            $"Time passes... it is now {currentDate.WeekdayName}, hour {currentDate.Hour}.";

        return Results.Ok(new WaitResponse(message));
    }

    private static async Task<IResult> EndSession(
        Guid sessionId,
        GameSessionStateStore sessionStore,
        SessionTerminator sessionTerminator,
        CancellationToken cancellationToken
    )
    {
        var state = sessionStore.Get(sessionId);
        if (state == null)
        {
            return Results.NotFound();
        }

        await sessionTerminator.EndSession(sessionId, state, cancellationToken);
        return Results.NoContent();
    }

    private static TurnMetricsDto ToDto(TurnMetrics metrics) =>
        new(metrics.FirstTokenMs, metrics.TotalMs, metrics.TokenCount, metrics.TokensPerSecond);
}
