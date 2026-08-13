using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using TRPG.Application.Common;
using TRPG.Application.Common.Events;
using TRPG.Application.Common.Tools;
using TRPG.Application.Creatures.Commands;
using TRPG.Application.GameTurns;
using TRPG.Application.Scenes;
using TRPG.Application.Scenes.Queries;
using TRPG.Application.Worlds.Encounters;
using TRPG.Contracts.Encounters.Responses;

namespace TRPG.Application.Tools;

internal record MoveToolResult(SceneResult Scene, HostileEncounterState? Encounter);

internal class MoveTool(
    GameTurnContext turnContext,
    IGameClientEventSink gameEvents,
    GetSceneWithCatchUpQueryHandler getSceneWithCatchUp,
    MovePlayerCommandHandler movePlayer,
    ILogger<MoveTool> logger
) : IGameTool
{
    public Delegate Invoke => InvokeAsync;

    [DisplayName("move")]
    [Description(
        "Moves the player to a destination by exact name and returns the full scene there — do not call look after moving. When outdoors, pass the exact Name of a building from NearbyBuildings to enter it, or the exact DestinationRoomName of an exit from Exits to travel to an adjacent district. When indoors, pass the exact DestinationRoomName of an exit from Exits to travel through it (this includes the literal value \"Outside\" for exits that lead outdoors). The name must be copied verbatim from the most recent look or move result — never invented, guessed, or paraphrased, and never a name you have not actually seen in a tool result this session."
    )]
    private async Task<object?> InvokeAsync(
        [Description(
            "The exact Name of a nearby building, or the exact DestinationRoomName of an exit (the literal value \"Outside\" for exits leading outdoors, or an adjacent district's name), copied verbatim from the most recent look or move result."
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

        turnContext.PlayerMoved = true;

        var scene = await getSceneWithCatchUp.Handle(
            new GetSceneWithCatchUpQuery
            {
                WorldId = turnContext.WorldId,
                PlayerId = turnContext.PlayerId,
                SessionId = turnContext.SessionId,
            },
            cancellationToken
        );

        gameEvents.Enqueue(
            new SceneUpdatedEvent(SceneSnapshotMapper.ToSnapshot(scene!), SceneUpdateReason.Moved)
        );

        if (moveResult.Encounter != null)
        {
            gameEvents.Enqueue(new EncounterStartedEvent(moveResult.Encounter));
        }

        var result = new MoveToolResult(scene!, moveResult.Encounter);

        logger.LogInformation(
            "[perf] [move] result in {ElapsedMs}ms: {Result}",
            stopwatch.ElapsedMilliseconds,
            JsonSerializer.Serialize(result, ToolJsonOptions.Options)
        );
        return result;
    }

    private static ToolError? ToToolError(EntryOutcome outcome, string destinationName) =>
        outcome switch
        {
            EntryOutcome.Entered => null,
            EntryOutcome.NoEntrance => new ToolError(
                $"'{destinationName}' has no entrance. Call look to see what's around."
            ),
            EntryOutcome.Locked => new ToolError($"The door to '{destinationName}' is locked."),
            EntryOutcome.DestinationNotFound => new ToolError(
                $"No building or district named '{destinationName}' found nearby. Call look to see what's around."
            ),
            EntryOutcome.ExitNotFound => new ToolError(
                $"No exit named '{destinationName}' found here. Call look to see the available exits."
            ),
            EntryOutcome.EncounterActive => new ToolError(
                "A hostile encounter is already underway — resolve it before moving."
            ),
            _ => throw new ArgumentOutOfRangeException(nameof(outcome)),
        };
}
