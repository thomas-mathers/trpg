using System.Net.WebSockets;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OllamaSharp;
using OllamaSharp.Models;
using TRPG.Contracts;
using TRPG.Services;

namespace TRPG.Endpoints;

internal static class SessionEndpoints {
    public static void MapSessionEndpoints(this WebApplication app) {
        app.MapPost("/worlds/{worldId:guid}/sessions", StartSession);
        app.MapPost("/sessions/{sessionId:guid}/chat", SendChat);
        app.Map("/sessions/{sessionId:guid}/chat/stream", ChatStream);
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

    private static async Task ChatStream(
        HttpContext httpContext,
        Guid sessionId,
        GameSessionStore sessionStore,
        IServiceScopeFactory scopeFactory,
        WorldService worldService,
        ILogger<GameTurnRunner> logger) {
        if (!httpContext.WebSockets.IsWebSocketRequest) {
            httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        var state = sessionStore.Get(sessionId);
        if (state == null) {
            httpContext.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        using var socket = await httpContext.WebSockets.AcceptWebSocketAsync();
        var cancellationToken = httpContext.RequestAborted;

        try {
            await StreamTurn(socket, state, scopeFactory,
                turnRunner => turnRunner.SendOpeningStreaming(cancellationToken), cancellationToken);

            while (socket.State == WebSocketState.Open) {
                var request = await ReceiveMessage<ChatRequest>(socket, cancellationToken);
                if (request == null) {
                    break;
                }

                await StreamTurn(socket, state, scopeFactory,
                    turnRunner => turnRunner.ProcessTurnStreaming(request.Message, cancellationToken),
                    cancellationToken);
            }

            if (socket.State == WebSocketState.CloseReceived) {
                await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, null, cancellationToken);
            }
        }
        catch (WebSocketException ex) {
            logger.LogInformation(ex, "[chat-stream] socket disconnected for session {SessionId}", sessionId);
        }
        catch (OperationCanceledException) {
            logger.LogInformation("[chat-stream] request aborted for session {SessionId}", sessionId);
        }
        finally {
            await EndSessionCleanup(sessionId, state, sessionStore, worldService, CancellationToken.None);
        }
    }

    private static async Task StreamTurn(
        WebSocket socket,
        GameSessionState state,
        IServiceScopeFactory scopeFactory,
        Func<GameTurnRunner, IAsyncEnumerable<string>> runTurn,
        CancellationToken cancellationToken) {
        await using var scope = scopeFactory.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<CurrentGameSessionAccessor>().State = state;
        var turnRunner = scope.ServiceProvider.GetRequiredService<GameTurnRunner>();

        await foreach (var token in runTurn(turnRunner)) {
            await SendMessage(socket, new ChatStreamToken(token), cancellationToken);
        }

        await SendMessage(socket, new ChatStreamDone(), cancellationToken);
    }

    private static async Task<T?> ReceiveMessage<T>(WebSocket socket, CancellationToken cancellationToken) {
        using var stream = new MemoryStream();
        var buffer = new byte[4096];
        WebSocketReceiveResult result;
        do {
            result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
            if (result.MessageType == WebSocketMessageType.Close) {
                return default;
            }

            await stream.WriteAsync(buffer.AsMemory(0, result.Count), cancellationToken);
        } while (!result.EndOfMessage);

        stream.Position = 0;
        return await JsonSerializer.DeserializeAsync<T>(stream, JsonSerializerOptions.Web, cancellationToken);
    }

    private static async Task SendMessage(WebSocket socket, ChatStreamMessage message, CancellationToken cancellationToken) {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(message, JsonSerializerOptions.Web);
        await socket.SendAsync(bytes, WebSocketMessageType.Text, true, cancellationToken);
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
