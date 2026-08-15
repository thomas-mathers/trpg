using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using TRPG.Application.Common.Events;
using TRPG.Application.Common.Tools;
using TRPG.Application.GameTurns;
using TRPG.Application.GameTurns.Events;
using TRPG.Application.Scenes;
using TRPG.Application.Scenes.Commands;
using TRPG.Application.Scenes.Mappers;

namespace TRPG.GameTurns.Tools;

internal class LookTool(
    GameTurnContext turnContext,
    IGameClientEventSink gameEvents,
    RefreshSceneCommandHandler refreshScene,
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

        var refreshed = await refreshScene.Handle(
            new RefreshSceneCommand
            {
                WorldId = turnContext.WorldId,
                PlayerId = turnContext.PlayerId,
                SessionId = turnContext.SessionId,
            },
            cancellationToken
        );

        if (refreshed.Refreshed)
        {
            gameEvents.Enqueue(
                new SceneUpdatedEvent(SceneSnapshotMapper.ToSnapshot(refreshed.Scene))
            );
        }

        logger.LogInformation(
            "[perf] [look] result in {ElapsedMs}ms: {Result}",
            stopwatch.ElapsedMilliseconds,
            JsonSerializer.Serialize(refreshed.Scene, TRPG.Contracts.TrpgJsonOptions.Default)
        );
        return refreshed.Scene;
    }
}
