using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.GameSessions;
using TRPG.Application.GameSessions.Commands;
using TRPG.GameSessions.Requests;
using TRPG.GameSessions.Responses;

namespace TRPG.Admin.Endpoints;

internal static class AdminEndpoints
{
    public static void MapAdminEndpoints(this WebApplication app)
    {
        app.MapPost("/admin/sessions/{sessionId:guid}/chat", SendChat);
        app.MapPost("/admin/sessions/{sessionId:guid}/wait", Wait);
        app.MapDelete("/admin/sessions/{sessionId:guid}", EndSession);
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
        return TypedResults.Ok(
            new ChatResponse(metrics.Response, includeMetrics == true ? ToDto(metrics) : null)
        );
    }

    private static async Task<IResult> Wait(
        Guid sessionId,
        WaitRequest request,
        AdvanceTimeCommandHandler advanceTime,
        CancellationToken cancellationToken
    )
    {
        if (request.Hours <= 0)
        {
            return TypedResults.BadRequest();
        }

        var bankedPlaytime = await advanceTime.Handle(
            new AdvanceTimeCommand
            {
                SessionId = sessionId,
                Delta = request.Hours * GameClock.RealTimePerInGameHour,
            },
            cancellationToken
        );

        var currentDate = GameClock.GetCurrentInGameDate(bankedPlaytime);
        var message =
            $"Time passes... it is now {currentDate.WeekdayName}, hour {currentDate.Hour}.";
        return TypedResults.Ok(new WaitResponse(message));
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
        return TypedResults.NoContent();
    }

    private static TurnMetricsDto ToDto(TurnMetrics metrics) =>
        new(metrics.FirstTokenMs, metrics.TotalMs, metrics.TokenCount, metrics.TokensPerSecond);
}
