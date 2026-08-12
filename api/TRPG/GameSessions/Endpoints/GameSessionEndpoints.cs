using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using TRPG.Application.GameSessions;
using TRPG.Application.GameSessions.Commands;
using TRPG.Application.GameSessions.Queries;
using TRPG.Application.Scenes;
using TRPG.Application.Scenes.Queries;
using TRPG.Application.Worlds.Queries;
using TRPG.Contracts.Combat.Requests;
using TRPG.Contracts.Combat.Responses;
using TRPG.Contracts.GameSessions.Requests;
using TRPG.Contracts.GameSessions.Responses;
using TRPG.Contracts.Scenes.Responses;
using TRPG.GameSessions.Hubs;

namespace TRPG.GameSessions.Endpoints;

internal static class GameSessionEndpoints
{
    public static void MapGameSessionEndpoints(this WebApplication app)
    {
        app.MapPost("/sessions", StartSession).WithName("CreateSession");
        app.MapPost("/sessions/{sessionId:guid}/combat-actions", ResolveCombatAction)
            .WithName("ResolveCombatAction");
        app.MapGet("/sessions/{sessionId:guid}/scene", GetScene).WithName("GetSessionScene");
        app.MapGet("/sessions/{sessionId:guid}/lore-anchors", GetLoreAnchors)
            .WithName("ListSessionLoreAnchors");
        app.MapGet("/sessions/{sessionId:guid}/lore-anchors/{anchorId:guid}", GetLoreAnchorById)
            .WithName("GetSessionLoreAnchor");
    }

    private static async Task<Ok<CombatActionResponse>> ResolveCombatAction(
        Guid sessionId,
        PlayerCombatAction action,
        GameTurnRunner turnRunner,
        GameTurnContext turnContext,
        GameClientEventDispatcher eventDispatcher,
        GetGameSessionQueryHandler getGameSession,
        GetSceneWithCatchUpQueryHandler getSceneWithCatchUp,
        CancellationToken cancellationToken
    )
    {
        var session = await getGameSession.Handle(
            new GetGameSessionQuery { SessionId = sessionId },
            cancellationToken
        );
        turnContext.SessionId = session.Id;

        var response = await turnRunner.ResolveCombatAction(action, cancellationToken);
        var scene = await getSceneWithCatchUp.Handle(
            new GetSceneWithCatchUpQuery
            {
                WorldId = session.WorldId,
                PlayerId = session.PlayerId,
                SessionId = session.Id,
            },
            cancellationToken
        );
        await eventDispatcher.FlushAsync(session.WorldId, cancellationToken);
        return TypedResults.Ok(
            response with
            {
                Scene = scene is null ? null : SceneSnapshotMapper.ToSnapshot(scene),
            }
        );
    }

    private static async Task<Results<NotFound, Ok<SessionCreatedResponse>>> StartSession(
        CreateSessionRequest request,
        GetWorldQueryHandler getWorld,
        CreateGameSessionCommandHandler createGameSession,
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
                Playtime = world.Playtime,
            },
            cancellationToken
        );

        return TypedResults.Ok(new SessionCreatedResponse(sessionId, world.PlayerId.Value));
    }

    private static async Task<Results<NotFound, Ok<SceneSnapshot>>> GetScene(
        Guid sessionId,
        GetGameSessionQueryHandler getGameSession,
        GetSceneWithCatchUpQueryHandler getSceneWithCatchUp,
        CancellationToken cancellationToken
    )
    {
        var session = await getGameSession.Handle(
            new GetGameSessionQuery { SessionId = sessionId },
            cancellationToken
        );

        var scene = await getSceneWithCatchUp.Handle(
            new GetSceneWithCatchUpQuery
            {
                WorldId = session.WorldId,
                PlayerId = session.PlayerId,
                SessionId = sessionId,
            },
            cancellationToken
        );
        if (scene == null)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Ok(SceneSnapshotMapper.ToSnapshot(scene));
    }

    private static async Task<Ok<LoreAnchor[]>> GetLoreAnchors(
        Guid sessionId,
        GetGameSessionQueryHandler getGameSession,
        GetLoreAnchorsByWorldQueryHandler getLoreAnchorsByWorld,
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

        return TypedResults.Ok(anchors.Select(ToLoreAnchor).ToArray());
    }

    private static async Task<Results<NotFound, Ok<LoreAnchor>>> GetLoreAnchorById(
        Guid sessionId,
        Guid anchorId,
        GetGameSessionQueryHandler getGameSession,
        GetLoreAnchorsByWorldQueryHandler getLoreAnchorsByWorld,
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
        return anchor == null ? TypedResults.NotFound() : TypedResults.Ok(ToLoreAnchor(anchor));
    }

    private static LoreAnchor ToLoreAnchor(LoreAnchorSummary anchor) =>
        new(
            anchor.Id,
            anchor.Name,
            anchor.Type switch
            {
                LoreAnchorType.Creature => EntityType.Creature,
                LoreAnchorType.Building => EntityType.Building,
                LoreAnchorType.District => EntityType.District,
                LoreAnchorType.World => EntityType.World,
                LoreAnchorType.Country => EntityType.Country,
                LoreAnchorType.State => EntityType.State,
                LoreAnchorType.City => EntityType.City,
                _ => throw new ArgumentOutOfRangeException(nameof(anchor)),
            },
            anchor.Subtype,
            anchor.Description
        );
}
