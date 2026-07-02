using Microsoft.Extensions.Logging;
using OllamaSharp.Models.Chat;
using OllamaSharp.Tools;
using TRPG.Services;

namespace TRPG.Tools;

internal class MoveTool : Tool, IInvokableTool {
    private readonly GameSession _session;
    private readonly SceneService _sceneService;
    private readonly PersonService _personService;
    private readonly BuildingService _buildingService;
    private readonly ILogger<MoveTool> _logger;

    public MoveTool(GameSession session, SceneService sceneService, PersonService personService, BuildingService buildingService, ILogger<MoveTool> logger) {
        _session = session;
        _sceneService = sceneService;
        _personService = personService;
        _buildingService = buildingService;
        _logger = logger;

        Function = new Function {
            Name = "move",
            Description = "Moves the player to a destination by exact name and returns the full scene there — do not call look after moving. When outdoors, pass the exact Name of a building from NearbyBuildings to enter it. When indoors, pass the exact DestinationRoomName of an exit from Room.Exits to travel through it (this includes the literal value \"Outside\" for exits that lead outdoors). The name must be copied verbatim from the most recent look or move result — never invented, guessed, or paraphrased, and never a name you have not actually seen in a tool result this session.",
            Parameters = new Parameters {
                Type = "object",
                Properties = new Dictionary<string, Property> {
                    ["destinationName"] = new() {
                        Type = "string",
                        Description = "The exact Name of a nearby building, or the exact DestinationRoomName of an exit (the literal value \"Outside\" for exits leading outdoors), copied verbatim from the most recent look or move result."
                    }
                },
                Required = new List<string> { "destinationName" }
            }
        };
    }

    private async Task<object?> InvokeMethodAsync(string destinationName, CancellationToken cancellationToken) {
        var player = await _personService.GetById(_session.PlayerId, cancellationToken);

        if (player!.RoomId == null) {
            var building = await _buildingService.GetByNameInRegion(player.RegionId, destinationName, cancellationToken);
            if (building == null) {
                return new { Error = $"No building named '{destinationName}' found nearby. Call look to see what's around." };
            }

            var entranceRoom = await _buildingService.GetEntranceRoom(building.Id, cancellationToken);
            if (entranceRoom == null) {
                return new { Error = $"'{destinationName}' has no entrance. Call look to see what's around." };
            }

            player.RoomId = entranceRoom.Id;
        } else {
            var exitMatch = await _buildingService.FindExitByDestinationName(player.RoomId.Value, destinationName, cancellationToken);
            if (!exitMatch.Matched) {
                return new { Error = $"No exit named '{destinationName}' found here. Call look to see the available exits." };
            }

            player.RoomId = exitMatch.DestinationRoomId;
        }

        await _personService.Update(player, cancellationToken);

        return await _sceneService.GetScene(_session.WorldId, _session.PlayerId, cancellationToken);
    }

    public object? InvokeMethod(IDictionary<string, object?>? args) {
        if (args is null || !args.TryGetValue("destinationName", out var destinationNameRaw) ||
            string.IsNullOrWhiteSpace(destinationNameRaw?.ToString())) {
            return new { Error = "No destinationName provided. Call look to get valid destination names." };
        }

        var destinationName = destinationNameRaw.ToString()!;

        _logger.LogDebug("[move] destinationName={DestinationName}", destinationName);
        var result = InvokeMethodAsync(destinationName, CancellationToken.None).GetAwaiter().GetResult();
        var json = System.Text.Json.JsonSerializer.Serialize(result, ToolJsonOptions.Options);
        _logger.LogDebug("[move] result: {Result}", json);
        return json;
    }
}
