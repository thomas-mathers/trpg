using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.Game;
using TRPG.Application.Game.Queries;
using TRPG.Application.Scenes.Queries;
using TRPG.Application.Tools.Common;

namespace TRPG.Application.Tools;

internal class LookTool(
    GameTurnContext turnContext,
    GetSceneWithCatchUpQueryHandler getSceneWithCatchUp,
    GetCreatureByIdQueryHandler getCreatureById,
    GetPlaytimeQueryHandler getPlaytime,
    ILogger<LookTool> logger
) : IGameTool
{
    public Delegate Invoke => InvokeAsync;

    [DisplayName("look")]
    [Description(
        "Returns everything currently observable at the player's location: CurrentDate (Year, MonthName, Day, WeekdayName, and a 24-hour Hour where 0 is midnight); the current region; the building and room (with its exits) if indoors; nearby props and people; nearby ordinary buildings (shops, homes, civic buildings); and nearby dungeons (caves, crypts, mines, ruins, towers — hostile, monster-filled sites) as a separate list, so narrate them as dangerous, unlike the NearbyBuildings list. Both are only populated outdoors — empty indoors because you can't see outside from in here, not because the city has no buildings. Call this before narrating any location, and again after anything might have changed what's nearby."
    )]
    private async Task<object?> InvokeAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("[look] tool invoked");
        var stopwatch = Stopwatch.StartNew();

        var playtime = await getPlaytime.Handle(
            new GetPlaytimeQuery { SessionId = turnContext.SessionId },
            cancellationToken
        );
        var currentDate = GameClock.GetCurrentInGameDate(playtime);
        var player = await getCreatureById.Handle(
            new GetCreatureByIdQuery { Id = turnContext.PlayerId },
            cancellationToken
        );
        var result = await getSceneWithCatchUp.Handle(
            new GetSceneWithCatchUpQuery
            {
                WorldId = turnContext.WorldId,
                PlayerId = turnContext.PlayerId,
                RoomId = player!.RoomId,
                DistrictId = player.DistrictId,
                StateId = player.StateId,
                CurrentDate = currentDate,
            },
            cancellationToken
        );

        turnContext.DidSceneRefreshThisTurn = true;
        logger.LogInformation(
            "[perf] [look] result in {ElapsedMs}ms: {Result}",
            stopwatch.ElapsedMilliseconds,
            JsonSerializer.Serialize(result, ToolJsonOptions.Options)
        );
        return result;
    }
}
