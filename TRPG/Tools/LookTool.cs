using System.Text.Json;
using Microsoft.Extensions.Logging;
using OllamaSharp.Models.Chat;
using OllamaSharp.Tools;
using TRPG.Services;

namespace TRPG.Tools;

internal class LookTool : Tool, IInvokableTool {
    private readonly ILogger<LookTool> _logger;
    private readonly SceneService _sceneService;
    private readonly GameSession _session;

    public LookTool(GameSession session, SceneService sceneService, ILogger<LookTool> logger) {
        _session = session;
        _sceneService = sceneService;
        _logger = logger;

        Function = new Function {
            Name = "look",
            Description =
                "Returns everything currently observable at the player's location: the current region; the building and room (with its exits) if indoors; nearby props and people; and nearby buildings if outdoors. Call this before narrating any location, and again after anything might have changed what's nearby.",
            Parameters = new Parameters {
                Type = "object",
                Properties = new Dictionary<string, Property>(),
                Required = new List<string>()
            }
        };
    }

    public object? InvokeMethod(IDictionary<string, object?>? args) {
        _logger.LogInformation("[look] tool invoked");
        var currentDate = GameClock.GetCurrentInGameDate(_session);
        var query = new SceneQuery(_session.WorldId, _session.PlayerId, currentDate);
        var result = _sceneService.GetScene(query).GetAwaiter().GetResult();
        var json = JsonSerializer.Serialize(result, ToolJsonOptions.Options);
        _logger.LogInformation("[look] result: {Result}", json);
        return json;
    }
}