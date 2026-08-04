using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using TRPG.Application.Common.Tools;
using TRPG.Application.Creatures.Commands;
using TRPG.Application.GameSessions;
using TRPG.Application.GameSessions.Queries;
using TRPG.Application.Scenes;
using TRPG.Application.Scenes.Queries;

namespace TRPG.Application.Tools;

internal class MoveTool(
    GameTurnContext turnContext,
    GetSceneWithCatchUpQueryHandler getSceneWithCatchUp,
    MovePlayerCommandHandler movePlayer,
    GetPlaytimeQueryHandler getPlaytime,
    ILogger<MoveTool> logger
) : IGameTool
{
    public Delegate Invoke => InvokeAsync;

    [DisplayName("move")]
    [Description(
        "Moves the player to a destination by exact name and returns the full scene there — do not call look after moving. When outdoors, pass the exact Name of a building from NearbyBuildings or a dungeon from NearbyDungeons to enter it, or the exact DestinationRoomName of an exit from Exits to travel to an adjacent district. When indoors, pass the exact DestinationRoomName of an exit from Exits to travel through it (this includes the literal value \"Outside\" for exits that lead outdoors). The name must be copied verbatim from the most recent look or move result — never invented, guessed, or paraphrased, and never a name you have not actually seen in a tool result this session."
    )]
    private async Task<object?> InvokeAsync(
        [Description(
            "The exact Name of a nearby building or dungeon, or the exact DestinationRoomName of an exit (the literal value \"Outside\" for exits leading outdoors, or an adjacent district's name), copied verbatim from the most recent look or move result."
        )]
            string destinationName,
        CancellationToken cancellationToken
    )
    {
        logger.LogInformation("[move] destinationName={DestinationName}", destinationName);
        var stopwatch = Stopwatch.StartNew();

        var moveResult = await movePlayer.Handle(
            new MovePlayerCommand
            {
                PlayerId = turnContext.PlayerId,
                SessionId = turnContext.SessionId,
                DestinationName = destinationName,
            },
            cancellationToken
        );

        var error = ToToolError(moveResult.Outcome, destinationName);
        if (error != null)
        {
            return error;
        }

        var player = moveResult.Player;
        var playtime = await getPlaytime.Handle(
            new GetPlaytimeQuery { SessionId = turnContext.SessionId },
            cancellationToken
        );
        var currentDate = GameClock.GetCurrentInGameDate(playtime);
        var result = await getSceneWithCatchUp.Handle(
            new GetSceneWithCatchUpQuery
            {
                WorldId = turnContext.WorldId,
                PlayerId = turnContext.PlayerId,
                LocationId = player.LocationId,
                CurrentDate = currentDate,
            },
            cancellationToken
        );

        turnContext.PendingEvents.Enqueue(
            new SceneUpdatedEvent(SceneSnapshotMapper.ToSnapshot(result), SceneUpdateReason.Moved)
        );
        logger.LogInformation(
            "[perf] [move] result in {ElapsedMs}ms: {Result}",
            stopwatch.ElapsedMilliseconds,
            JsonSerializer.Serialize(result, ToolJsonOptions.Options)
        );
        return result;
    }

    private static ToolError? ToToolError(MovePlayerOutcome outcome, string destinationName) =>
        outcome switch
        {
            MovePlayerOutcome.Moved => null,
            MovePlayerOutcome.BuildingHasNoEntrance => new ToolError(
                $"'{destinationName}' has no entrance. Call look to see what's around."
            ),
            MovePlayerOutcome.DoorLocked => new ToolError(
                $"The door to '{destinationName}' is locked."
            ),
            MovePlayerOutcome.DestinationNotFound => new ToolError(
                $"No building or district named '{destinationName}' found nearby. Call look to see what's around."
            ),
            MovePlayerOutcome.ExitNotFound => new ToolError(
                $"No exit named '{destinationName}' found here. Call look to see the available exits."
            ),
            _ => throw new ArgumentOutOfRangeException(nameof(outcome)),
        };
}
