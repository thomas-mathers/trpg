using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using TRPG.Application.Common.Mappers;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.GameSessions;
using TRPG.Application.GameSessions.Commands;
using TRPG.Application.GameSessions.Queries;
using TRPG.Application.Scenes.Queries;
using TRPG.Application.Worlds.Queries;
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
    }

    private static async Task<IResult> StartSession(
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
            return Results.NotFound();
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

        return Results.Ok(new CreateSessionResponse(sessionId, world.PlayerId.Value));
    }

    private static async Task<IResult> GetScene(
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
            return Results.NotFound();
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

        return Results.Ok(ToSnapshot(scene));
    }

    private static async Task<IResult> GetNamedEntities(
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

        return Results.Ok(entities.Select(ToNamedEntity).ToArray());
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
                NamedEntityType.Item => EntityType.Item,
                NamedEntityType.World => EntityType.World,
                NamedEntityType.Country => EntityType.Country,
                NamedEntityType.State => EntityType.State,
                NamedEntityType.City => EntityType.City,
                _ => throw new ArgumentOutOfRangeException(nameof(entity)),
            }
        );

    private static SceneSnapshot ToSnapshot(SceneResult scene)
    {
        var currentDistrict = scene.City?.Districts.FirstOrDefault(d => d.IsCurrent);
        return new SceneSnapshot(
            StateName: scene.State?.Name ?? "",
            CityName: scene.City?.Name,
            DistrictName: currentDistrict?.Name,
            BuildingName: scene.Building?.Name,
            RoomName: scene.Room?.Name,
            Year: scene.CurrentDate.Year,
            MonthName: scene.CurrentDate.MonthName,
            Day: scene.CurrentDate.Day,
            WeekdayName: scene.CurrentDate.WeekdayName,
            Hour: scene.CurrentDate.Hour,
            PlayerStatus: ToCreatureStatusSnapshot(scene.Player),
            NearbyCreatures: scene.NearbyCreatures.Select(ToCreatureStatusSnapshot).ToArray(),
            NearbyDistricts: scene
                .City?.Districts.Select(d => new NearbyDistrictSnapshot(d.Name, d.Type.ToContract()))
                .ToArray()
                ?? [],
            NearbyBuildings: scene
                .NearbyBuildings.Select(b => new NearbyBuildingSnapshot(b.Name, b.Type.ToContract()))
                .ToArray(),
            NearbyDungeons: scene
                .NearbyDungeons.Select(b => new NearbyBuildingSnapshot(b.Name, b.Type.ToContract()))
                .ToArray(),
            NearbyProps: scene
                .NearbyProps.Select(p => new NearbyPropSnapshot(p.Name, p.Type))
                .ToArray(),
            Exits: scene
                .Room?.Exits.Select(e => new NearbyExitSnapshot(
                    e.Description,
                    e.DestinationRoomName
                ))
                .ToArray()
                ?? []
        );
    }

    private static CreatureStatusSnapshot ToCreatureStatusSnapshot(SceneCreatureInfo creature) =>
        new(
            Name: creature.Name,
            CreatureType: creature.CreatureType.ToContract(),
            Gender: creature.Gender.ToContract(),
            Profession: creature.Profession?.ToContract(),
            Level: creature.Level,
            Age: creature.Age,
            State: creature.State?.ToContract(),
            Gold: creature.Gold,
            CurrentHp: creature.CurrentHp,
            MaximumHp: creature.MaximumHp,
            CurrentAp: creature.CurrentAp,
            MaximumAp: creature.MaximumAp,
            CurrentMp: creature.CurrentMp,
            MaximumMp: creature.MaximumMp,
            ExperienceCurrent: creature.ExperienceCurrent,
            ExperienceToNextLevel: creature.ExperienceToNextLevel,
            FactionNames: creature.FactionNames,
            Reputation: creature.Reputation
        );
}
