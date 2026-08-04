using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using TRPG.Application.Common.Tools;
using TRPG.Application.GameSessions;
using TRPG.Application.Scenes.Queries;

namespace TRPG.Application.Tools;

internal class LookTool(
    GameTurnContext turnContext,
    GetSceneWithCatchUpQueryHandler getSceneWithCatchUp,
    ILogger<LookTool> logger
) : IGameTool
{
    public Delegate Invoke => InvokeAsync;

    [DisplayName("look")]
    [Description(
        "Returns everything currently observable at the player's location: CurrentDate (Year, MonthName, Day, WeekdayName, and a 24-hour Hour where 0 is midnight); the current region; the building and room (with its exits) if indoors; nearby props and people; and nearby buildings, both ordinary (shops, homes, civic buildings) and dungeons (caves, crypts, mines, ruins, towers — hostile, monster-filled sites, identifiable by Type; narrate these as dangerous). NearbyBuildings is only populated outdoors — empty indoors because you can't see outside from in here, not because the city has no buildings. Call this before narrating any location, and again after anything might have changed what's nearby."
    )]
    private async Task<object?> InvokeAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("[look] tool invoked");
        var stopwatch = Stopwatch.StartNew();

        var result = await getSceneWithCatchUp.Handle(
            new GetSceneWithCatchUpQuery
            {
                WorldId = turnContext.WorldId,
                PlayerId = turnContext.PlayerId,
                SessionId = turnContext.SessionId,
            },
            cancellationToken
        );

        logger.LogInformation(
            "[perf] [look] result in {ElapsedMs}ms: {Result}",
            stopwatch.ElapsedMilliseconds,
            JsonSerializer.Serialize(result, ToolJsonOptions.Options)
        );
        return result;
    }
}
