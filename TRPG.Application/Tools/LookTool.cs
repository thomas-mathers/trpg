using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.Game;
using TRPG.Application.Scenes.Queries;

namespace TRPG.Application.Tools;

internal class LookTool(
    GameSession session,
    GetSceneWithCatchUpQueryHandler getSceneWithCatchUp,
    GetCreatureByIdQueryHandler getCreatureById,
    ILogger<LookTool> logger
) : IGameTool
{
    public Delegate Invoke => InvokeAsync;

    [DisplayName("look")]
    [Description(
        "Returns everything currently observable at the player's location: CurrentDate (Year, MonthName, Day, WeekdayName, and a 24-hour Hour where 0 is midnight); the current region; the building and room (with its exits) if indoors; nearby props and people; and nearby buildings (only populated outdoors — empty indoors because you can't see outside from in here, not because the city has no buildings). Call this before narrating any location, and again after anything might have changed what's nearby."
    )]
    private async Task<object?> InvokeAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("[look] tool invoked");
        var stopwatch = Stopwatch.StartNew();

        var currentDate = GameClock.GetCurrentInGameDate(session);
        var player = await getCreatureById.Handle(
            new GetCreatureByIdQuery { Id = session.PlayerId },
            cancellationToken
        );
        var result = await getSceneWithCatchUp.Handle(
            new GetSceneWithCatchUpQuery
            {
                Session = session,
                RoomId = player!.RoomId,
                DistrictId = player.DistrictId,
                CurrentDate = currentDate,
            },
            cancellationToken
        );

        session.DidSceneRefreshThisTurn = true;
        logger.LogInformation(
            "[perf] [look] result in {ElapsedMs}ms: {Result}",
            stopwatch.ElapsedMilliseconds,
            JsonSerializer.Serialize(result, ToolJsonOptions.Options)
        );
        return result;
    }
}
