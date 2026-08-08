using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.SignalR;
using TRPG.Application.GameSessions;
using TRPG.Application.GameSessions.Commands;
using TRPG.Application.GameSessions.Queries;
using TRPG.Application.Scenes;
using TRPG.Application.Scenes.Queries;
using TRPG.Application.Worlds.Queries;
using TRPG.Contracts;
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
        app.MapGet("/sessions/{sessionId:guid}/named-entities", GetNamedEntities)
            .WithName("ListSessionNamedEntities");
        app.MapGet("/sessions/{sessionId:guid}/named-entities/{entityId:guid}", GetNamedEntityById)
            .WithName("GetSessionNamedEntity");
    }

    private static async Task<Ok<CombatActionResponse>> ResolveCombatAction(
        Guid sessionId,
        PlayerCombatAction action,
        GameTurnRunner turnRunner,
        GameTurnContext turnContext,
        IGameClientEventDispatcher eventDispatcher,
        IHubContext<ChatHub> chatHub,
        WorldConnectionRegistry worldConnections,
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
        if (worldConnections.TryGetConnectionId(session.WorldId, out var connectionId))
        {
            while (eventDispatcher.TryDequeue(out var gameEvent))
            {
                await chatHub
                    .Clients.Client(connectionId!)
                    .SendAsync(gameEvent!.MethodName, gameEvent.Payload, cancellationToken);
            }
        }
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

    private static async Task<Ok<NamedEntity[]>> GetNamedEntities(
        Guid sessionId,
        GetGameSessionQueryHandler getGameSession,
        GetNamedEntitiesByWorldQueryHandler getNamedEntitiesByWorld,
        CancellationToken cancellationToken
    )
    {
        var session = await getGameSession.Handle(
            new GetGameSessionQuery { SessionId = sessionId },
            cancellationToken
        );

        var entities = await getNamedEntitiesByWorld.Handle(
            new GetNamedEntitiesByWorldQuery { WorldId = session.WorldId },
            cancellationToken
        );

        return TypedResults.Ok(entities.Select(ToNamedEntity).ToArray());
    }

    private static async Task<Results<NotFound, Ok<NamedEntity>>> GetNamedEntityById(
        Guid sessionId,
        Guid entityId,
        GetGameSessionQueryHandler getGameSession,
        GetNamedEntitiesByWorldQueryHandler getNamedEntitiesByWorld,
        CancellationToken cancellationToken
    )
    {
        var session = await getGameSession.Handle(
            new GetGameSessionQuery { SessionId = sessionId },
            cancellationToken
        );

        var entities = await getNamedEntitiesByWorld.Handle(
            new GetNamedEntitiesByWorldQuery { WorldId = session.WorldId },
            cancellationToken
        );

        var entity = entities.FirstOrDefault(e => e.Id == entityId);
        return entity == null ? TypedResults.NotFound() : TypedResults.Ok(ToNamedEntity(entity));
    }

    private static NamedEntity ToNamedEntity(NamedEntitySummary entity) =>
        new(
            entity.Id,
            entity.Name,
            entity.Type switch
            {
                NamedEntityType.Creature => EntityType.Creature,
                NamedEntityType.Building => EntityType.Building,
                NamedEntityType.District => EntityType.District,
                NamedEntityType.World => EntityType.World,
                NamedEntityType.Country => EntityType.Country,
                NamedEntityType.State => EntityType.State,
                NamedEntityType.City => EntityType.City,
                _ => throw new ArgumentOutOfRangeException(nameof(entity)),
            },
            entity.Subtype,
            entity.Description
        );
}
