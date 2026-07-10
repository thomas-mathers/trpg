using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using OllamaSharp.Models.Chat;
using OllamaSharp.Tools;
using TRPG.Application.Game;
using TRPG.Application.Worlds.Queries;

namespace TRPG.Application.Tools;

internal class WorldInfoTool : Tool, IInvokableTool
{
    private readonly GameSession _session;
    private readonly GetWorldQueryHandler _getWorld;
    private readonly ILogger<WorldInfoTool> _logger;

    public WorldInfoTool(
        GameSession session,
        GetWorldQueryHandler getWorld,
        ILogger<WorldInfoTool> logger
    )
    {
        _session = session;
        _getWorld = getWorld;
        _logger = logger;

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
        _logger.LogInformation("[world] tool invoked");
        var stopwatch = Stopwatch.StartNew();
        var world = _getWorld
            .Handle(new GetWorldQuery { WorldId = _session.WorldId }, CancellationToken.None)
            .GetAwaiter()
            .GetResult();
        var json = JsonSerializer.Serialize(
            new { world!.Name, world.Description },
            ToolJsonOptions.Options
        );
        _logger.LogInformation(
            "[perf] [world] result in {ElapsedMs}ms: {Result}",
            stopwatch.ElapsedMilliseconds,
            json
        );
        return json;
    }
}
