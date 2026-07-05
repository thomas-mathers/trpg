using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using OllamaSharp;
using OllamaSharp.Models;
using TRPG.Contracts;
using TRPG.Services;

namespace TRPG.Endpoints;

internal static class SessionEndpoints {
    public static void MapSessionEndpoints(this WebApplication app) {
        app.MapPost("/worlds/{worldId:guid}/sessions", StartSession);
        app.MapPost("/sessions/{sessionId:guid}/chat", SendChat);
        app.MapPost("/sessions/{sessionId:guid}/wait", Wait);
        app.MapDelete("/sessions/{sessionId:guid}", EndSession);
    }

    private static async Task<IResult> StartSession(
        Guid worldId,
        bool? includeMetrics,
        WorldService worldService,
        IOllamaApiClient ollamaClient,
        AppConfiguration appConfiguration,
        GameSessionStore sessionStore,
        HttpContext httpContext,
        CancellationToken cancellationToken) {
        var world = await worldService.GetWorld(worldId, cancellationToken);
        if (world?.PlayerId == null) {
            return Results.NotFound();
        }

        var session = new GameSession(worldId, world.PlayerId.Value, world.Playtime);
        var chat = new Chat(ollamaClient, GameTurnRunner.SystemPrompt) {
            Think = appConfiguration.OllamaThink,
            Options = new RequestOptions { NumCtx = 8192, Temperature = appConfiguration.OllamaTemperature }
        };
        var state = new GameSessionState(session, chat);
        var sessionId = sessionStore.Add(state);

        httpContext.RequestServices.GetRequiredService<CurrentGameSessionAccessor>().State = state;
        var turnRunner = httpContext.RequestServices.GetRequiredService<GameTurnRunner>();
        var metrics = await turnRunner.SendOpening(cancellationToken);

        return Results.Ok(new CreateSessionResponse(sessionId, metrics.Response,
            includeMetrics == true ? ToDto(metrics) : null));
    }

    private static async Task<IResult> SendChat(
        Guid sessionId,
        ChatRequest request,
        bool? includeMetrics,
        GameSessionStore sessionStore,
        HttpContext httpContext,
        CancellationToken cancellationToken) {
        var state = sessionStore.Get(sessionId);
        if (state == null) {
            return Results.NotFound();
        }

        httpContext.RequestServices.GetRequiredService<CurrentGameSessionAccessor>().State = state;
        var turnRunner = httpContext.RequestServices.GetRequiredService<GameTurnRunner>();
        var metrics = await turnRunner.ProcessTurn(request.Message, cancellationToken);

        return Results.Ok(new ChatResponse(metrics.Response, includeMetrics == true ? ToDto(metrics) : null));
    }

    private static async Task<IResult> Wait(
        Guid sessionId,
        WaitRequest request,
        GameSessionStore sessionStore,
        SceneService sceneService,
        CancellationToken cancellationToken) {
        var state = sessionStore.Get(sessionId);
        if (state == null) {
            return Results.NotFound();
        }

        if (request.Hours <= 0) {
            return Results.BadRequest();
        }

        GameClock.AdvanceHours(state.Session, request.Hours);
        var currentDate = GameClock.GetCurrentInGameDate(state.Session);
        var query = new SceneQuery(state.Session.WorldId, state.Session.PlayerId, currentDate);
        var scene = await sceneService.GetScene(query, cancellationToken);
        var message = $"Time passes... it is now {currentDate.WeekdayName}, hour {currentDate.Hour}.";

        return Results.Ok(new WaitResponse(message, scene));
    }

    private static async Task<IResult> EndSession(
        Guid sessionId,
        GameSessionStore sessionStore,
        WorldService worldService,
        CancellationToken cancellationToken) {
        var state = sessionStore.Get(sessionId);
        if (state == null) {
            return Results.NotFound();
        }

        var world = await worldService.GetWorld(state.Session.WorldId, cancellationToken);
        world!.Playtime = GameClock.GetTotalPlaytime(state.Session);
        await worldService.Update(world, cancellationToken);

        sessionStore.Remove(sessionId);
        return Results.NoContent();
    }

    private static TurnMetricsDto ToDto(TurnMetrics metrics) =>
        new(metrics.FirstTokenMs, metrics.TotalMs, metrics.TokenCount, metrics.TokensPerSecond);
}
