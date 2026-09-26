using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Queries;
using TRPG.Application.GameSessions.Commands;
using TRPG.Application.GameSessions.Queries;
using TRPG.Application.GameTurns.Commands;
using TRPG.Application.Narration.Queries;
using TRPG.Application.Narration.Results;
using TRPG.Application.Worlds.Commands;
using TRPG.Application.Worlds.Queries;
using TRPG.Domain;
using TRPG.Domain.Models;
using TRPG.GameSessions.Mappers;
using TRPG.GameSessions.Requests;
using TRPG.GameSessions.Responses;

namespace TRPG.GameSessions.Endpoints;

internal static class GameSessionEndpoints
{
    public static void MapGameSessionEndpoints(this WebApplication app)
    {
        app.MapPost("/sessions", StartSession).WithName("CreateSession");
        app.MapGet("/sessions/{sessionId:guid}/scene", GetScene).WithName("GetSessionScene");
        app.MapGet("/sessions/{sessionId:guid}/lore-anchors", GetLoreAnchors)
            .WithName("ListSessionLoreAnchors");
        app.MapGet("/sessions/{sessionId:guid}/lore-anchors/{anchorId:guid}", GetLoreAnchorById)
            .WithName("GetSessionLoreAnchor");
    }

    private static async Task<Results<NotFound, Ok<SessionCreatedResponse>>> StartSession(
        CreateSessionRequest request,
        [FromServices] IQueryHandler<GetWorldQuery, World?> getWorld,
        [FromServices] ICommandHandler<CreateGameSessionCommand, Guid> createGameSession,
        CancellationToken cancellationToken
    )
    {
        var world = await getWorld.Handle(
            new GetWorldQuery { WorldId = request.WorldId },
            cancellationToken
        );
        if (world?.PlayerId == null)
        {
            return TypedResults.NotFound();
        }

        var sessionId = await createGameSession.Handle(
            new CreateGameSessionCommand
            {
                WorldId = request.WorldId,
                PlayerId = world.PlayerId.Value,
            },
            cancellationToken
        );

        return TypedResults.Ok(new SessionCreatedResponse(sessionId, world.PlayerId.Value));
    }

    private static async Task<Results<NotFound, Ok<SceneSnapshot>>> GetScene(
        Guid sessionId,
        [FromServices] IQueryHandler<GetGameSessionQuery, GameSession> getGameSession,
        [FromServices] IQueryHandler<GetGameTimeQuery, GameInstant> getGameTime,
        [FromServices] ICommandHandler<RefreshSceneCommand, RefreshSceneResult> refreshScene,
        [FromServices] ICommandHandler<StampWorldStateCommand, WorldStateStamp> stampWorldState,
        CancellationToken cancellationToken
    )
    {
        var session = await getGameSession.Handle(
            new GetGameSessionQuery { SessionId = sessionId },
            cancellationToken
        );

        // Stamped before the scene is read so a later-numbered snapshot never describes older state.
        var stamp = await stampWorldState.Handle(
            new StampWorldStateCommand { WorldId = session.WorldId },
            cancellationToken
        );

        var gameTime = await getGameTime.Handle(
            new GetGameTimeQuery { SessionId = sessionId },
            cancellationToken
        );

        var refreshed = await refreshScene.Handle(
            new RefreshSceneCommand
            {
                WorldId = session.WorldId,
                PlayerId = session.PlayerId,
                GameTime = gameTime,
            },
            cancellationToken
        );

        return TypedResults.Ok(refreshed.Scene.ToSnapshot(stamp));
    }

    private static async Task<Ok<LoreAnchor[]>> GetLoreAnchors(
        Guid sessionId,
        [FromServices] IQueryHandler<GetGameSessionQuery, GameSession> getGameSession,
        [FromServices]
            IQueryHandler<
            GetLoreAnchorsByWorldQuery,
            IReadOnlyCollection<LoreAnchorResult>
        > getLoreAnchorsByWorld,
        CancellationToken cancellationToken
    )
    {
        var session = await getGameSession.Handle(
            new GetGameSessionQuery { SessionId = sessionId },
            cancellationToken
        );

        var anchors = await getLoreAnchorsByWorld.Handle(
            new GetLoreAnchorsByWorldQuery { WorldId = session.WorldId },
            cancellationToken
        );

        return TypedResults.Ok(anchors.Select(anchor => anchor.ToResponse()).ToArray());
    }

    private static async Task<Results<NotFound, Ok<LoreAnchor>>> GetLoreAnchorById(
        Guid sessionId,
        Guid anchorId,
        [FromServices] IQueryHandler<GetGameSessionQuery, GameSession> getGameSession,
        [FromServices]
            IQueryHandler<
            GetLoreAnchorsByWorldQuery,
            IReadOnlyCollection<LoreAnchorResult>
        > getLoreAnchorsByWorld,
        CancellationToken cancellationToken
    )
    {
        var session = await getGameSession.Handle(
            new GetGameSessionQuery { SessionId = sessionId },
            cancellationToken
        );

        var anchors = await getLoreAnchorsByWorld.Handle(
            new GetLoreAnchorsByWorldQuery { WorldId = session.WorldId },
            cancellationToken
        );

        var anchor = anchors.FirstOrDefault(anchor => anchor.Id == anchorId);
        return anchor == null ? TypedResults.NotFound() : TypedResults.Ok(anchor.ToResponse());
    }
}
