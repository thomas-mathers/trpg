using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.GameSessions;
using TRPG.Application.GameSessions.Commands;
using TRPG.Application.GameSessions.Queries;
using TRPG.Application.Scenes.Queries;
using TRPG.Application.Worlds.Queries;
using TRPG.Contracts.GameSessions.Responses;
using TRPG.Contracts.Scenes.Responses;
using TRPG.Data.Models;

namespace TRPG.GameSessions.Endpoints;

internal static class GameSessionEndpoints
{
    public static void MapGameSessionEndpoints(this WebApplication app)
    {
        app.MapPost("/sessions", StartSession);
        app.MapGet("/sessions/{sessionId:guid}/scene", GetScene);
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

        return Results.Ok(new CreateSessionResponse(sessionId));
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

        return Results.Ok(ToSnapshot(scene, player));
    }

    private static SceneSnapshot ToSnapshot(SceneResult scene, Creature player)
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
            PlayerStatus: new CreatureStatusSnapshot(
                Name: player.Name,
                CreatureType: player.CreatureType.ToString(),
                Gender: player.Gender.ToString(),
                Profession: player.Profession?.ToString() ?? "",
                Level: player.Level,
                Age: scene.CurrentDate.Year - player.BirthYear,
                State: player.State == CreatureState.Dead ? player.State.ToString() : null,
                Gold: player.Gold,
                CurrentHp: player.CurrentHp,
                MaximumHp: player.Attributes.MaximumHp,
                CurrentAp: player.CurrentAp,
                MaximumAp: player.Attributes.MaximumAp,
                CurrentMp: player.CurrentMp,
                MaximumMp: player.Attributes.MaximumMp,
                FactionNames: null,
                Reputation: null
            ),
            NearbyCreatures: scene
                .NearbyCreatures.Select(p => new CreatureStatusSnapshot(
                    Name: p.Name,
                    CreatureType: p.CreatureType,
                    Gender: p.Gender,
                    Profession: p.Profession,
                    Level: p.Level,
                    Age: p.Age,
                    State: p.State,
                    Gold: p.Gold,
                    CurrentHp: p.CurrentHp,
                    MaximumHp: p.MaximumHp,
                    CurrentAp: p.CurrentAp,
                    MaximumAp: p.MaximumAp,
                    CurrentMp: p.CurrentMp,
                    MaximumMp: p.MaximumMp,
                    FactionNames: p.FactionNames,
                    Reputation: p.Reputation
                ))
                .ToArray(),
            NearbyDistricts: scene
                .City?.Districts.Select(d => new NearbyDistrictSnapshot(d.Name, d.Type))
                .ToArray()
                ?? [],
            NearbyBuildings: scene
                .NearbyBuildings.Select(b => new NearbyBuildingSnapshot(b.Name, b.Type))
                .ToArray(),
            NearbyDungeons: scene
                .NearbyDungeons.Select(b => new NearbyBuildingSnapshot(b.Name, b.Type))
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
}
