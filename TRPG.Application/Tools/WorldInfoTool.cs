using System.Text.Json;
using OllamaSharp.Models.Chat;
using OllamaSharp.Tools;
using TRPG.Application.Game;
using TRPG.Application.Worlds.Queries;

namespace TRPG.Application.Tools;

internal class WorldInfoTool : Tool, IInvokableTool
{
    private readonly GameSession _session;
    private readonly GetWorldQueryHandler _getWorld;

    public WorldInfoTool(GameSession session, GetWorldQueryHandler getWorld)
    {
        _session = session;
        _getWorld = getWorld;

        Function = new Function
        {
            Name = "world",
            Description =
                "Returns the world's name and lore description — its tone, culture, and history. Call this when you need background beyond the current scene, such as narrating rumors, festivals, or a character's cultural origin.",
            Parameters = new Parameters
            {
                Type = "object",
                Properties = new Dictionary<string, Property>(),
                Required = new List<string>(),
            },
        };
    }

    public object? InvokeMethod(IDictionary<string, object?>? args)
    {
        var world = _getWorld
            .Handle(new GetWorldQuery { WorldId = _session.WorldId }, CancellationToken.None)
            .GetAwaiter()
            .GetResult();
        return JsonSerializer.Serialize(
            new { world!.Name, world.Description },
            ToolJsonOptions.Options
        );
    }
}
