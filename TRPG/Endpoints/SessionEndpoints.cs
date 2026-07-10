using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using OllamaSharp;
using OllamaSharp.Models;
using TRPG.Application.Game;
using TRPG.Application.Worlds.Commands;
using TRPG.Application.Worlds.Queries;
using TRPG.Contracts;

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
        IOllamaApiClient ollamaClient,
        AppConfiguration appConfiguration,
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
        var chat = new Chat(ollamaClient, GameTurnRunner.SystemPrompt)
        {
            Think = appConfiguration.OllamaThink,
            Options = new RequestOptions
            {
                NumCtx = 8192,
                Temperature = appConfiguration.OllamaTemperature,
            },
        };
        var state = new GameSessionState(session, chat);
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

        httpContext
            .RequestServices.GetRequiredService<CurrentGameSessionStateAccessor>()
            .State = state;
        var turnRunner = httpContext.RequestServices.GetRequiredService<GameTurnRunner>();
        var metrics = await turnRunner.ProcessTurn(request.Message, cancellationToken);

        return Results.Ok(
            new ChatResponse(metrics.Response, includeMetrics == true ? ToDto(metrics) : null)
        );
    }

    private static IResult Wait(Guid sessionId, WaitRequest request, GameSessionStateStore sessionStore)
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

        GameClock.AdvanceHours(state.Session, request.Hours);
        var currentDate = GameClock.GetCurrentInGameDate(state.Session);
        var message =
            $"Time passes... it is now {currentDate.WeekdayName}, hour {currentDate.Hour}.";

        return Results.Ok(new WaitResponse(message));
    }

    private static async Task<IResult> EndSession(
        Guid sessionId,
        GameSessionStateStore sessionStore,
        GetWorldQueryHandler getWorld,
        UpdateWorldCommandHandler updateWorld,
        CancellationToken cancellationToken
    )
    {
        var state = sessionStore.Get(sessionId);
        if (state == null)
        {
            return Results.NotFound();
        }

        var world = await getWorld.Handle(
            new GetWorldQuery { WorldId = state.Session.WorldId },
            cancellationToken
        );
        world!.Playtime = GameClock.GetTotalPlaytime(state.Session);
        await updateWorld.Handle(new UpdateWorldCommand { World = world }, cancellationToken);

        sessionStore.Remove(sessionId);
        return Results.NoContent();
    }

    private static TurnMetricsDto ToDto(TurnMetrics metrics) =>
        new(metrics.FirstTokenMs, metrics.TotalMs, metrics.TokenCount, metrics.TokensPerSecond);
}
