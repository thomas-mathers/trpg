using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using TRPG.Application.Game;
using TRPG.Application.Worlds.Queries;

namespace TRPG.Application.Tools;

internal class WorldInfoTool(
    GameSession session,
    GetWorldQueryHandler getWorld,
    ILogger<WorldInfoTool> logger
) : IGameTool
{
    public Delegate Invoke => InvokeAsync;

    [DisplayName("world")]
    [Description(
        "Returns the world's name and lore description — its tone, culture, and history. Call this when you need background beyond the current scene, such as narrating rumors, festivals, or a character's cultural origin."
    )]
    private async Task<object?> InvokeAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("[world] tool invoked");
        var stopwatch = Stopwatch.StartNew();

        var world = await getWorld.Handle(
            new GetWorldQuery { WorldId = session.WorldId },
            cancellationToken
        );
        var result = new { world!.Name, world.Description };

        logger.LogInformation(
            "[perf] [world] result in {ElapsedMs}ms: {Result}",
            stopwatch.ElapsedMilliseconds,
            JsonSerializer.Serialize(result, ToolJsonOptions.Options)
        );
        return result;
    }
}
