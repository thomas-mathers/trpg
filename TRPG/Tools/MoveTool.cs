using System.Text.Json;
using Microsoft.Extensions.Logging;
using OllamaSharp.Models.Chat;
using OllamaSharp.Tools;
using TRPG.Models;
using TRPG.Services;

namespace TRPG.Tools;

internal class MoveTool : Tool, IInvokableTool
{
    private readonly CurrentGameSessionAccessor _currentGameSessionAccessor;
    private readonly BuildingService _buildingService;
    private readonly CreatureService _creatureService;
    private readonly SceneSyncService _sceneSyncService;
    private readonly LocationService _locationService;
    private readonly LockService _lockService;
    private readonly ILogger<MoveTool> _logger;
    private readonly SceneService _sceneService;
    private readonly GameSession _session;

    public MoveTool(
        GameSession session,
        SceneService sceneService,
        CreatureService creatureService,
        BuildingService buildingService,
        LocationService locationService,
        LockService lockService,
        SceneSyncService sceneSyncService,
        CurrentGameSessionAccessor currentGameSessionAccessor,
        ILogger<MoveTool> logger
    )
    {
        _session = session;
        _sceneService = sceneService;
        _creatureService = creatureService;
        _buildingService = buildingService;
        _locationService = locationService;
        _lockService = lockService;
        _sceneSyncService = sceneSyncService;
        _currentGameSessionAccessor = currentGameSessionAccessor;
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
        var result = InvokeMethodAsync(destinationName, CancellationToken.None)
            .GetAwaiter()
            .GetResult();
        var json = JsonSerializer.Serialize(result, ToolJsonOptions.Options);
        _logger.LogInformation("[move] result: {Result}", json);
        return json;
    }

    private async Task<object?> InvokeMethodAsync(
        string destinationName,
        CancellationToken cancellationToken
    )
    {
        var player = await _creatureService.GetById(_session.PlayerId, cancellationToken);

        var error =
            player!.RoomId == null
                ? await MoveOutdoors(player, destinationName, cancellationToken)
                : await MoveIndoors(player, destinationName, cancellationToken);

        if (error != null)
        {
            return error;
        }

        await _creatureService.Update(player, cancellationToken);
        _session.DidMoveThisTurn = true;

        var currentDate = GameClock.GetCurrentInGameDate(_session);
        var state = _currentGameSessionAccessor.State;
        var sceneIsStale = await _sceneSyncService.SyncIfNeeded(
            _session,
            _session.WorldId,
            player.RoomId,
            player.DistrictId,
            currentDate,
            cancellationToken
        );

        SceneResult result;
        if (!sceneIsStale && state.LastScene != null)
        {
            result = state.LastScene;
        }
        else
        {
            var query = new SceneQuery(_session.WorldId, _session.PlayerId, currentDate);
            result = await _sceneService.GetScene(query, cancellationToken);
            state.LastScene = result;
        }

        _session.SceneRefreshedThisTurn = true;
        return result;
    }

    private async Task<object?> MoveOutdoors(
        Creature player,
        string destinationName,
        CancellationToken cancellationToken
    )
    {
        var building = await _buildingService.GetByNameInState(
            player.StateId,
            destinationName,
            cancellationToken
        );
        if (building != null)
        {
            var entranceRoom = await _buildingService.GetEntranceRoom(
                building.Id,
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
            await _sceneSyncService.SyncScheduleLock(
                building.Id,
                building.BuildingType,
                currentDate,
                cancellationToken
            );
            var canEnter = await _lockService.CanEnter(
                entranceRoom.Id,
                player.Id,
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
                ? await _locationService.GetDistrictByNameInCity(
                    player.CityId.Value,
                    destinationName,
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
        var exitMatch = await _buildingService.FindExitByDestinationName(
            player.RoomId!.Value,
            destinationName,
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
