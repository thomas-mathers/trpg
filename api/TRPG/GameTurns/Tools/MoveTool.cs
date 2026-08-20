using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Events;
using TRPG.Application.Common.Queries;
using TRPG.Application.Encounters.Events;
using TRPG.Application.GameTurns;
using TRPG.Application.GameTurns.Commands;
using TRPG.Application.Inventory;
using TRPG.Application.Inventory.Queries;
using TRPG.Application.Scenes.Queries;
using TRPG.Domain.Models;
using TRPG.Tools;

namespace TRPG.GameTurns.Tools;

internal record MoveToolEncounterMember(string Name, CreatureType CreatureType, int Level);

internal record MoveToolEncounter(
    string FactionName,
    string LocationName,
    IReadOnlyCollection<MoveToolEncounterMember> Members
);

internal record MoveToolGuardEncounter(
    string GuardName,
    string LocationName,
    int FineAmount,
    int JailHours,
    IReadOnlyCollection<string> RecentOffenses
);

internal record MoveToolResult(
    SceneResult Scene,
    MoveToolEncounter? Encounter,
    MoveToolGuardEncounter? GuardEncounter
);

internal class MoveTool(
    GameTurnContext turnContext,
    IGameClientEventSink gameEvents,
    ICommandHandler<MovePlayerCommand, MovePlayerResult> movePlayer,
    IQueryHandler<GetGoldQuantityQuery, int> getGoldQuantity,
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

        var scene = moveResult.Scene!;

        MoveToolEncounter? encounterSummary = null;
        if (moveResult.Encounter is HostileEncounter hostileEncounter)
        {
            gameEvents.Enqueue(new HostileEncounterStartedEvent(hostileEncounter));
            encounterSummary = new MoveToolEncounter(
                hostileEncounter.FactionName,
                hostileEncounter.LocationName!,
                hostileEncounter
                    .Members.Select(member => new MoveToolEncounterMember(
                        member.Name,
                        member.CreatureType,
                        member.Level
                    ))
                    .ToArray()
            );
        }

        MoveToolGuardEncounter? guardEncounterSummary = null;
        if (moveResult.GuardEncounter is { } guardEncounter)
        {
            var playerGold = await getGoldQuantity.Handle(
                new GetGoldQuantityQuery
                {
                    Owner = new ItemOwnerReference(turnContext.PlayerId, OwnerType.Creature),
                },
                cancellationToken
            );
            gameEvents.Enqueue(
                new GuardEncounterStartedEvent(
                    guardEncounter,
                    playerGold >= guardEncounter.FineAmount
                )
            );
            guardEncounterSummary = new MoveToolGuardEncounter(
                guardEncounter.GuardName,
                guardEncounter.LocationName!,
                guardEncounter.FineAmount,
                guardEncounter.JailHours,
                guardEncounter.RecentOffenses
            );
        }

        var result = new MoveToolResult(scene, encounterSummary, guardEncounterSummary);

        logger.LogInformation(
            "[perf] [move] result in {ElapsedMs}ms: {Result}",
            stopwatch.ElapsedMilliseconds,
            JsonSerializer.Serialize(
                result,
                TRPG.Application.Common.Serialization.TrpgJsonOptions.Default
            )
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
