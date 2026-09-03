using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Queries;
using TRPG.Application.Encounters.Queries;
using TRPG.Application.GameTurns;
using TRPG.Application.GameTurns.Commands;
using TRPG.Application.GameTurns.Results;
using TRPG.Domain.Models;
using TRPG.GameTurns.Mappers;
using TRPG.Tools;

namespace TRPG.GameTurns.Tools;

internal record MoveToolEncounterMember(string Name, CreatureType CreatureType, int Level);

internal record MoveToolHostileEncounter(
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

internal record MoveToolOverdueKeyEncounter(
    string ConfrontingName,
    IReadOnlyCollection<string> ItemNames
);

internal record MoveToolResult(
    SceneResult Scene,
    MoveToolHostileEncounter? HostileEncounter,
    MoveToolGuardEncounter? GuardEncounter,
    MoveToolOverdueKeyEncounter? OverdueRoomKeyEncounter
);

internal class MoveTool(
    GameTurnContext turnContext,
    IQueryHandler<GetActiveEncounterQuery, Encounter?> getActiveEncounter,
    ICommandHandler<
        ResolveMoveDestinationCommand,
        ResolveMoveDestinationResult
    > resolveMoveDestination,
    ICommandHandler<MovePlayerCommand, MovePlayerResult> movePlayer,
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

        var activeEncounter = await getActiveEncounter.Handle(
            new GetActiveEncounterQuery { PlayerId = turnContext.PlayerId },
            cancellationToken
        );
        if (activeEncounter != null)
        {
            return EntryOutcome.EncounterActive.ToToolError(destinationName);
        }

        var destinationResult = await resolveMoveDestination.Handle(
            new ResolveMoveDestinationCommand
            {
                PlayerId = turnContext.PlayerId,
                SessionId = turnContext.SessionId,
                DestinationName = destinationName,
            },
            cancellationToken
        );

        var error = destinationResult.Outcome.ToToolError(destinationName);
        if (error != null)
        {
            return error;
        }

        var moveResult = await movePlayer.Handle(
            new MovePlayerCommand
            {
                PlayerId = turnContext.PlayerId,
                SessionId = turnContext.SessionId,
                DestinationLocationId = destinationResult.DestinationLocationId!.Value,
            },
            cancellationToken
        );

        turnContext.PlayerMoved = true;

        var scene = moveResult.Scene;

        var result = new MoveToolResult(
            scene,
            moveResult.Encounter?.ToMoveToolSummary()
                ?? moveResult.TrespassingEncounter?.ToMoveToolSummary(),
            moveResult.GuardEncounter?.ToMoveToolSummary(),
            moveResult.OverdueRoomKeyEncounter?.ToMoveToolSummary()
        );

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
}
