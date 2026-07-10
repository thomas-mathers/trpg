using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using OllamaSharp.Models.Chat;
using OllamaSharp.Tools;
using TRPG.Application.Buildings.Queries;
using TRPG.Application.Creatures.Commands;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.Game;
using TRPG.Application.Scenes.Commands;
using TRPG.Application.Scenes.Queries;
using TRPG.Application.Worlds.Queries;
using TRPG.Data.Models;

namespace TRPG.Application.Tools;

internal class MoveTool : Tool, IInvokableTool
{
    private readonly GetBuildingByNameInStateQueryHandler _getBuildingByNameInState;
    private readonly GetEntranceRoomQueryHandler _getEntranceRoom;
    private readonly GetExitByDestinationNameQueryHandler _getExitByDestinationName;
    private readonly GetCreatureByIdQueryHandler _getCreatureById;
    private readonly UpdateCreatureCommandHandler _updateCreature;
    private readonly GetSceneWithCatchUpQueryHandler _getSceneWithCatchUp;
    private readonly SyncScheduleLockCommandHandler _syncScheduleLock;
    private readonly GetDistrictByNameInCityQueryHandler _getDistrictByNameInCity;
    private readonly CanEnterBuildingQueryHandler _canEnterBuilding;
    private readonly ILogger<MoveTool> _logger;
    private readonly GameSession _session;

    public MoveTool(
        GameSession session,
        GetSceneWithCatchUpQueryHandler getSceneWithCatchUp,
        GetCreatureByIdQueryHandler getCreatureById,
        UpdateCreatureCommandHandler updateCreature,
        GetBuildingByNameInStateQueryHandler getBuildingByNameInState,
        GetEntranceRoomQueryHandler getEntranceRoom,
        GetExitByDestinationNameQueryHandler getExitByDestinationName,
        GetDistrictByNameInCityQueryHandler getDistrictByNameInCity,
        CanEnterBuildingQueryHandler canEnterBuilding,
        SyncScheduleLockCommandHandler syncScheduleLock,
        ILogger<MoveTool> logger
    )
    {
        _session = session;
        _getSceneWithCatchUp = getSceneWithCatchUp;
        _getCreatureById = getCreatureById;
        _updateCreature = updateCreature;
        _getBuildingByNameInState = getBuildingByNameInState;
        _getEntranceRoom = getEntranceRoom;
        _getExitByDestinationName = getExitByDestinationName;
        _getDistrictByNameInCity = getDistrictByNameInCity;
        _canEnterBuilding = canEnterBuilding;
        _syncScheduleLock = syncScheduleLock;
        _logger = logger;

        Function = new Function
        {
            Name = "move",
            Description =
                "Moves the player to a destination by exact name and returns the full scene there — do not call look after moving. When outdoors, pass the exact Name of a building from NearbyBuildings to enter it, or the exact Name of a district from City.Districts to travel there. When indoors, pass the exact DestinationRoomName of an exit from Room.Exits to travel through it (this includes the literal value \"Outside\" for exits that lead outdoors). The name must be copied verbatim from the most recent look or move result — never invented, guessed, or paraphrased, and never a name you have not actually seen in a tool result this session.",
            Parameters = new Parameters
            {
                Type = "object",
                Properties = new Dictionary<string, Property>
                {
                    ["destinationName"] = new()
                    {
                        Type = "string",
                        Description =
                            "The exact Name of a nearby building, the exact Name of a district, or the exact DestinationRoomName of an exit (the literal value \"Outside\" for exits leading outdoors), copied verbatim from the most recent look or move result.",
                    },
                },
                Required = new List<string> { "destinationName" },
            },
        };
    }

    public object? InvokeMethod(IDictionary<string, object?>? args)
    {
        if (
            args is null
            || !args.TryGetValue("destinationName", out var destinationNameRaw)
            || string.IsNullOrWhiteSpace(destinationNameRaw?.ToString())
        )
        {
            return new
            {
                Error = "No destinationName provided. Call look to get valid destination names.",
            };
        }

        var destinationName = destinationNameRaw.ToString()!;

        _logger.LogInformation("[move] destinationName={DestinationName}", destinationName);
        var stopwatch = Stopwatch.StartNew();
        var result = InvokeMethodAsync(destinationName, CancellationToken.None)
            .GetAwaiter()
            .GetResult();
        var json = JsonSerializer.Serialize(result, ToolJsonOptions.Options);
        _logger.LogInformation(
            "[perf] [move] result in {ElapsedMs}ms: {Result}",
            stopwatch.ElapsedMilliseconds,
            json
        );
        return json;
    }

    private async Task<object?> InvokeMethodAsync(
        string destinationName,
        CancellationToken cancellationToken
    )
    {
        var player = await _getCreatureById.Handle(
            new GetCreatureByIdQuery { Id = _session.PlayerId },
            cancellationToken
        );

        var error =
            player!.RoomId == null
                ? await MoveOutdoors(player, destinationName, cancellationToken)
                : await MoveIndoors(player, destinationName, cancellationToken);

        if (error != null)
        {
            return error;
        }

        await _updateCreature.Handle(
            new UpdateCreatureCommand { Creature = player! },
            cancellationToken
        );
        _session.DidMoveThisTurn = true;

        var currentDate = GameClock.GetCurrentInGameDate(_session);
        var result = await _getSceneWithCatchUp.Handle(
            new GetSceneWithCatchUpQuery
            {
                Session = _session,
                RoomId = player.RoomId,
                DistrictId = player.DistrictId,
                CurrentDate = currentDate,
            },
            cancellationToken
        );

        _session.DidSceneRefreshThisTurn = true;
        return result;
    }

    private async Task<object?> MoveOutdoors(
        Creature player,
        string destinationName,
        CancellationToken cancellationToken
    )
    {
        var building = await _getBuildingByNameInState.Handle(
            new GetBuildingByNameInStateQuery { StateId = player.StateId, Name = destinationName },
            cancellationToken
        );
        if (building != null)
        {
            var entranceRoom = await _getEntranceRoom.Handle(
                new GetEntranceRoomQuery { BuildingId = building.Id },
                cancellationToken
            );
            if (entranceRoom == null)
            {
                return new
                {
                    Error = $"'{destinationName}' has no entrance. Call look to see what's around.",
                };
            }

            var currentDate = GameClock.GetCurrentInGameDate(_session);
            await _syncScheduleLock.Handle(
                new SyncScheduleLockCommand
                {
                    BuildingId = building.Id,
                    BuildingType = building.BuildingType,
                    CurrentDate = currentDate,
                },
                cancellationToken
            );
            var canEnter = await _canEnterBuilding.Handle(
                new CanEnterBuildingQuery
                {
                    EntranceRoomId = entranceRoom.Id,
                    EnteringCreatureId = player.Id,
                },
                cancellationToken
            );
            if (!canEnter)
            {
                return new { Error = $"The door to '{destinationName}' is locked." };
            }

            player.CityId = building.CityId;
            player.DistrictId = building.DistrictId;
            player.RoomId = entranceRoom.Id;
            return null;
        }

        var district =
            player.CityId != null
                ? await _getDistrictByNameInCity.Handle(
                    new GetDistrictByNameInCityQuery
                    {
                        CityId = player.CityId.Value,
                        Name = destinationName,
                    },
                    cancellationToken
                )
                : null;
        if (district != null)
        {
            player.DistrictId = district.Id;
            return null;
        }

        return new
        {
            Error = $"No building or district named '{destinationName}' found nearby. Call look to see what's around.",
        };
    }

    private async Task<object?> MoveIndoors(
        Creature player,
        string destinationName,
        CancellationToken cancellationToken
    )
    {
        var exitMatch = await _getExitByDestinationName.Handle(
            new GetExitByDestinationNameQuery
            {
                RoomId = player.RoomId!.Value,
                DestinationName = destinationName,
            },
            cancellationToken
        );
        if (!exitMatch.Matched)
        {
            return new
            {
                Error = $"No exit named '{destinationName}' found here. Call look to see the available exits.",
            };
        }

        player.RoomId = exitMatch.DestinationRoomId;
        return null;
    }
}
