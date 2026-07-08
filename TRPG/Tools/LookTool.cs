using System.Text.Json;
using Microsoft.Extensions.Logging;
using OllamaSharp.Models.Chat;
using OllamaSharp.Tools;
using TRPG.Services;

namespace TRPG.Tools;

internal class LookTool : Tool, IInvokableTool {
    private readonly CurrentGameSessionAccessor _currentGameSessionAccessor;
    private readonly CreatureService _creatureService;
    private readonly SceneSyncService _sceneSyncService;
    private readonly ILogger<LookTool> _logger;
    private readonly SceneService _sceneService;
    private readonly GameSession _session;

    public LookTool(GameSession session, SceneService sceneService, CreatureService creatureService,
        SceneSyncService sceneSyncService, CurrentGameSessionAccessor currentGameSessionAccessor,
        ILogger<LookTool> logger) {
        _session = session;
        _sceneService = sceneService;
        _creatureService = creatureService;
        _sceneSyncService = sceneSyncService;
        _currentGameSessionAccessor = currentGameSessionAccessor;
        _logger = logger;

        Function = new Function {
            Name = "look",
            Description =
                "Returns everything currently observable at the player's location: CurrentDate (Year, MonthName, Day, WeekdayName, and a 24-hour Hour where 0 is midnight); the current region; the building and room (with its exits) if indoors; nearby props and people; and nearby buildings (only populated outdoors — empty indoors because you can't see outside from in here, not because the city has no buildings). Call this before narrating any location, and again after anything might have changed what's nearby.",
            Parameters = new Parameters {
                Type = "object",
                Properties = new Dictionary<string, Property>(),
                Required = new List<string>()
            }
        };
    }

    public object? InvokeMethod(IDictionary<string, object?>? args) {
        _logger.LogInformation("[look] tool invoked");
        var result = InvokeMethodAsync(CancellationToken.None).GetAwaiter().GetResult();
        var json = JsonSerializer.Serialize(result, ToolJsonOptions.Options);
        _logger.LogInformation("[look] result: {Result}", json);
        return json;
    }

    private async Task<object?> InvokeMethodAsync(CancellationToken cancellationToken) {
        var currentDate = GameClock.GetCurrentInGameDate(_session);
        var player = await _creatureService.GetById(_session.PlayerId, cancellationToken);
        var state = _currentGameSessionAccessor.State;
        var sceneIsStale = await _sceneSyncService.SyncIfNeeded(_session, _session.WorldId, player!.RoomId,
            player.DistrictId, currentDate, cancellationToken);

        SceneResult result;
        if (!sceneIsStale && state.LastScene != null) {
            result = state.LastScene;
        }
        else {
            var query = new SceneQuery(_session.WorldId, _session.PlayerId, currentDate);
            result = await _sceneService.GetScene(query, cancellationToken);
            state.LastScene = result;
        }

        _session.SceneRefreshedThisTurn = true;
        return result;
    }
}