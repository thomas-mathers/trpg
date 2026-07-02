using OllamaSharp.Models.Chat;
using OllamaSharp.Tools;
using TRPG.Services;

namespace TRPG.Tools;

internal class WorldInfoTool : Tool, IInvokableTool {
    private readonly GameSession _session;
    private readonly WorldService _worldService;

    public WorldInfoTool(GameSession session, WorldService worldService) {
        _session = session;
        _worldService = worldService;

        Function = new Function {
            Name = "world",
            Description = "Returns the world's name and lore description — its tone, culture, and history. Call this when you need background beyond the current scene, such as narrating rumors, festivals, or a character's cultural origin.",
            Parameters = new Parameters {
                Type = "object",
                Properties = new Dictionary<string, Property>(),
                Required = new List<string>()
            }
        };
    }

    public object? InvokeMethod(IDictionary<string, object?>? args) {
        var world = _worldService.GetWorld(_session.WorldId, CancellationToken.None).GetAwaiter().GetResult();
        return System.Text.Json.JsonSerializer.Serialize(new { world!.Name, world.Description }, ToolJsonOptions.Options);
    }
}
