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
        WorldService worldService,
        IOllamaApiClient ollamaClient,
        AppConfiguration appConfiguration,
        GameSessionStore sessionStore,
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

        return Results.Ok(new CreateSessionResponse(sessionId));
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
        var message = $"Time passes... it is now {currentDate.WeekdayName}, hour {currentDate.Hour}.";

        return Results.Ok(new WaitResponse(message));
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

        await EndSessionCleanup(sessionId, state, sessionStore, worldService, cancellationToken);
        return Results.NoContent();
    }

    private static async Task EndSessionCleanup(
        Guid sessionId,
        GameSessionState state,
        GameSessionStore sessionStore,
        WorldService worldService,
        CancellationToken cancellationToken) {
        var world = await worldService.GetWorld(state.Session.WorldId, cancellationToken);
        world!.Playtime = GameClock.GetTotalPlaytime(state.Session);
        await worldService.Update(world, cancellationToken);

        sessionStore.Remove(sessionId);
    }

    private static TurnMetricsDto ToDto(TurnMetrics metrics) =>
        new(metrics.FirstTokenMs, metrics.TotalMs, metrics.TokenCount, metrics.TokensPerSecond);
}
