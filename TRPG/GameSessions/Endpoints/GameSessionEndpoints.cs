using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.GameSessions;
using TRPG.Application.GameSessions.Commands;
using TRPG.Application.GameSessions.Queries;
using TRPG.Application.Scenes;
using TRPG.Application.Scenes.Queries;
using TRPG.Application.Worlds.Queries;
using TRPG.Contracts;
using TRPG.Contracts.GameSessions.Responses;
using TRPG.Contracts.Scenes.Responses;

namespace TRPG.GameSessions.Endpoints;

internal static class GameSessionEndpoints
{
    public static void MapGameSessionEndpoints(this WebApplication app)
    {
        app.MapPost("/sessions", StartSession);
        app.MapGet("/sessions/{sessionId:guid}/scene", GetScene);
        app.MapGet("/sessions/{sessionId:guid}/named-entities", GetNamedEntities);
        app.MapGet("/sessions/{sessionId:guid}/named-entities/{entityId:guid}", GetNamedEntityById);
    }

    private static async Task<Results<NotFound, Ok<CreateSessionResponse>>> StartSession(
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
            return TypedResults.NotFound();
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

        return TypedResults.Ok(new CreateSessionResponse(sessionId, world.PlayerId.Value));
    }

    private static async Task<Results<NotFound, Ok<SceneSnapshot>>> GetScene(
        Guid sessionId,
        GetGameSessionQueryHandler getGameSession,
        GetCreatureByIdQueryHandler getCreatureById,
        GetSceneWithCatchUpQueryHandler getSceneWithCatchUp,
        CancellationToken cancellationToken
    )
    {
        var session = await getGameSession.Handle(
            new GetGameSessionQuery { SessionId = sessionId },
            cancellationToken
        );

        var player = await getCreatureById.Handle(
            new GetCreatureByIdQuery { Id = session.PlayerId },
            cancellationToken
        );
        if (player == null)
        {
            return TypedResults.NotFound();
        }

        var currentDate = GameClock.GetCurrentInGameDate(session.Playtime);
        var scene = await getSceneWithCatchUp.Handle(
            new GetSceneWithCatchUpQuery
            {
                WorldId = session.WorldId,
                PlayerId = session.PlayerId,
                RoomId = player.RoomId,
                DistrictId = player.DistrictId,
                StateId = player.StateId,
                CurrentDate = currentDate,
            },
            cancellationToken
        );

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
