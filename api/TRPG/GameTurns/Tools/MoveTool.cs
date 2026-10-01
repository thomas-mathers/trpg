using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Queries;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.GameTurns;
using TRPG.Application.GameTurns.Commands;
using TRPG.Application.Scenes.Mappers;
using TRPG.Application.Scenes.Results;
using TRPG.Application.Worlds.Queries;
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

internal record MoveToolShakedownEncounter(
    string FactionName,
    string LocationName,
    int TollAmount,
    IReadOnlyCollection<MoveToolEncounterMember> Members
);

internal record MoveToolGuardOffense(string Description, bool AgainstTheGuard);

internal record MoveToolGuardEncounter(
    string GuardName,
    string LocationName,
    int FineAmount,
    int JailHours,
    IReadOnlyCollection<MoveToolGuardOffense> RecentOffenses
);

internal record MoveToolOverdueKeyEncounter(
    string ConfrontingName,
    IReadOnlyCollection<string> ItemNames
);

internal record MoveToolSuspicionEncounter(string GuardName, string LocationName, string Reason);

internal record MoveToolResult(
    LlmScene Scene,
    MoveToolHostileEncounter? HostileEncounter,
    MoveToolShakedownEncounter? ShakedownEncounter,
    MoveToolGuardEncounter? GuardEncounter,
    MoveToolOverdueKeyEncounter? OverdueRoomKeyEncounter,
    MoveToolSuspicionEncounter? SuspicionEncounter,
    bool MoveInterrupted,
    string? TravelTime = null
);

internal class MoveTool(
    GameTurnContext turnContext,
    IQueryHandler<GetCreatureByIdQuery, Creature?> getCreatureById,
    IQueryHandler<GetExitByDestinationNameQuery, ExitMatch> getExitByDestinationName,
    ICommandHandler<ExecutePlayerMoveCommand, ExecutePlayerMoveResult> executePlayerMove,
    ILogger<MoveTool> logger
) : IGameTool
{
    public Delegate Invoke => InvokeAsync;

    [DisplayName("move")]
    [Description(
        "Moves the player to a destination by exact name and returns the full scene there — do not call look after moving. When outdoors, pass the exact Name of a building from NearbyBuildings to enter it, or the exact DestinationName of an exit from Exits to travel to an adjacent district. When indoors, pass the exact DestinationName of an exit from Exits to travel through it (this includes the literal value \"Outside\" for exits that lead outdoors). The name must be copied verbatim from the most recent look or move result — never invented, guessed, or paraphrased, and never a name you have not actually seen in a tool result this session. If this fails because the door is locked, just narrate that the door is locked — do not automatically call pick_lock; that requires the player to explicitly ask for it. When the result carries a guardEncounter, the named guard is stopping the player over the offences in RecentOffenses: any entry with AgainstTheGuard true was committed against that guard personally, so have them speak as the wronged party for those and as an officer of the city for the rest. When the result has moveInterrupted true, an encounter stopped the player before they left: they did not reach the destination and are still at the original location, so narrate the encounter and never describe an arrival. When the result carries a travelTime, that is exactly how long the trip took: narrate that duration and never work it out from the change in the date or hour, because time also passes while the player is idle."
    )]
    private async Task<object?> InvokeAsync(
        [Description(
            "The exact Name of a nearby building, or the exact DestinationName of an exit (the literal value \"Outside\" for exits leading outdoors, or an adjacent district's name), copied verbatim from the most recent look or move result."
        )]
            string destinationName,
        CancellationToken cancellationToken
    )
    {
        logger.LogInformation("[move] destinationName={DestinationName}", destinationName);
        var stopwatch = Stopwatch.StartNew();

        var player = await getCreatureById.Handle(
            new GetCreatureByIdQuery { Id = turnContext.PlayerId },
            cancellationToken
        );
        var exitMatch = await getExitByDestinationName.Handle(
            new GetExitByDestinationNameQuery
            {
                LocationId = player!.LocationId,
                DestinationName = destinationName,
            },
            cancellationToken
        );

        var moveResult = await executePlayerMove.Handle(
            new ExecutePlayerMoveCommand
            {
                SessionId = turnContext.SessionId,
                WorldId = turnContext.WorldId,
                PlayerId = turnContext.PlayerId,
                // An unmatched name has no connector, which the command rejects
                ConnectorId = exitMatch.ConnectorId ?? Guid.Empty,
            },
            cancellationToken
        );
        var result = ToToolResult(moveResult, destinationName);

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

    private object ToToolResult(ExecutePlayerMoveResult result, string destinationName)
    {
        if (result is MoveCompletedResult)
        {
            turnContext.PlayerMoved = true;
        }

        return result switch
        {
            MoveRejectedResult rejected => rejected.Outcome.ToToolError(destinationName)!,
            MoveTravelDeathResult => new ToolError(
                "The player died from a lingering effect during the journey and never arrived. Narrate their death."
            ),
            MoveInterruptedResult interrupted => BuildResult(
                interrupted.Scene,
                interrupted.Encounter,
                moveInterrupted: true
            ),
            MoveCompletedResult completed => BuildResult(
                completed.Scene,
                completed.Encounter,
                moveInterrupted: false,
                FormatTravelTime(completed.TravelTimeHours)
            ),
            _ => throw new ArgumentOutOfRangeException(nameof(result)),
        };
    }

    private static MoveToolResult BuildResult(
        SceneResult scene,
        Encounter? encounter,
        bool moveInterrupted,
        string? travelTime = null
    ) =>
        new(
            scene.ToLlmScene(),
            (encounter as HostileEncounter)?.ToMoveToolSummary(),
            (encounter as ShakedownEncounter)?.ToMoveToolSummary(),
            (encounter as GuardEncounter)?.ToMoveToolSummary(),
            (encounter as TheftEncounter)?.ToMoveToolSummary(),
            (encounter as SuspicionEncounter)?.ToMoveToolSummary(),
            moveInterrupted,
            travelTime
        );

    private static string? FormatTravelTime(double travelTimeHours)
    {
        if (travelTimeHours <= 0)
        {
            return null;
        }

        var totalMinutes = Math.Max(1, (int)Math.Round(travelTimeHours * 60));
        var parts = new List<string>();
        if (totalMinutes / 60 > 0)
        {
            parts.Add($"{totalMinutes / 60} {(totalMinutes / 60 == 1 ? "hour" : "hours")}");
        }
        if (totalMinutes % 60 > 0)
        {
            parts.Add($"{totalMinutes % 60} {(totalMinutes % 60 == 1 ? "minute" : "minutes")}");
        }

        return string.Join(" and ", parts);
    }
}
