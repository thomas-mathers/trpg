using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Exceptions;
using TRPG.Application.Common.Queries;
using TRPG.Application.Creatures.Commands;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.Encounters.Commands;
using TRPG.Application.Encounters.Queries;
using TRPG.Application.GameSessions.Queries;
using TRPG.Application.GameTurns;
using TRPG.Application.GameTurns.Commands;
using TRPG.Application.GameTurns.Queries;
using TRPG.Application.GameTurns.Results;
using TRPG.Domain;
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

internal record MoveToolSuspicionEncounter(string GuardName, string LocationName, string Reason);

internal record MoveToolResult(
    SceneResult Scene,
    MoveToolHostileEncounter? HostileEncounter,
    MoveToolGuardEncounter? GuardEncounter,
    MoveToolOverdueKeyEncounter? OverdueRoomKeyEncounter,
    MoveToolSuspicionEncounter? SuspicionEncounter
);

internal class MoveTool(
    GameTurnContext turnContext,
    IQueryHandler<GetActiveEncounterQuery, Encounter?> getActiveEncounter,
    ICommandHandler<
        ResolveMoveDestinationCommand,
        ResolveMoveDestinationResult
    > resolveMoveDestination,
    ICommandHandler<
        EvaluateMoveInterceptionCommand,
        EncounterEvaluationResult
    > evaluateMoveInterception,
    ICommandHandler<MovePlayerCommand> movePlayer,
    ICommandHandler<RefreshSceneCommand, RefreshSceneResult> refreshScene,
    ICommandHandler<PublishEncounterStartedCommand> publishEncounterStarted,
    IQueryHandler<GetSceneQuery, SceneResult> getScene,
    IQueryHandler<GetCreatureByIdQuery, Creature?> getCreatureById,
    IQueryHandler<GetPlaytimeQuery, TimeSpan> getPlaytime,
    ILogger<MoveTool> logger
) : IGameTool
{
    public Delegate Invoke => InvokeAsync;

    [DisplayName("move")]
    [Description(
        "Moves the player to a destination by exact name and returns the full scene there — do not call look after moving. When outdoors, pass the exact Name of a building from NearbyBuildings to enter it, or the exact DestinationRoomName of an exit from Exits to travel to an adjacent district. When indoors, pass the exact DestinationRoomName of an exit from Exits to travel through it (this includes the literal value \"Outside\" for exits that lead outdoors). The name must be copied verbatim from the most recent look or move result — never invented, guessed, or paraphrased, and never a name you have not actually seen in a tool result this session. If this fails because the door is locked, just narrate that the door is locked — do not automatically call pick_lock; that requires the player to explicitly ask for it."
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

        var player =
            await getCreatureById.Handle(
                new GetCreatureByIdQuery { Id = turnContext.PlayerId },
                cancellationToken
            ) ?? throw new EntityNotFoundException(nameof(Creature), turnContext.PlayerId);

        var playtime = await getPlaytime.Handle(
            new GetPlaytimeQuery { SessionId = turnContext.SessionId },
            cancellationToken
        );

        // Only a walk the player chose can be intercepted; being relocated by an encounter is not,
        // so this runs here rather than inside the move itself.
        var interception = await InterceptMove(
            player.LocationId,
            destinationResult.DestinationLocationId!.Value,
            playtime,
            cancellationToken
        );
        if (interception != null)
        {
            return interception;
        }

        await movePlayer.Handle(
            new MovePlayerCommand
            {
                PlayerId = turnContext.PlayerId,
                DestinationLocationId = destinationResult.DestinationLocationId!.Value,
                Playtime = playtime,
            },
            cancellationToken
        );

        turnContext.PlayerMoved = true;

        var startedEncounter = await getActiveEncounter.Handle(
            new GetActiveEncounterQuery { PlayerId = turnContext.PlayerId },
            cancellationToken
        );

        await publishEncounterStarted.Handle(
            new PublishEncounterStartedCommand
            {
                PlayerId = turnContext.PlayerId,
                Encounter = startedEncounter,
            },
            cancellationToken
        );

        var scene = await getScene.Handle(
            new GetSceneQuery
            {
                WorldId = turnContext.WorldId,
                PlayerId = turnContext.PlayerId,
                CurrentDate = GameClock.GetCurrentInGameDate(playtime),
            },
            cancellationToken
        );

        var result = BuildResult(scene, startedEncounter);

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

    private async Task<MoveToolResult?> InterceptMove(
        Guid fromLocationId,
        Guid destinationLocationId,
        TimeSpan playtime,
        CancellationToken cancellationToken
    )
    {
        var interception = await evaluateMoveInterception.Handle(
            new EvaluateMoveInterceptionCommand
            {
                WorldId = turnContext.WorldId,
                PlayerId = turnContext.PlayerId,
                FromLocationId = fromLocationId,
                ToLocationId = destinationLocationId,
                Playtime = playtime,
            },
            cancellationToken
        );
        if (interception.Encounter == null)
        {
            return null;
        }

        var refreshed = await refreshScene.Handle(
            new RefreshSceneCommand
            {
                WorldId = turnContext.WorldId,
                PlayerId = turnContext.PlayerId,
                Playtime = playtime,
            },
            cancellationToken
        );

        await publishEncounterStarted.Handle(
            new PublishEncounterStartedCommand
            {
                PlayerId = turnContext.PlayerId,
                Encounter = interception.Encounter,
            },
            cancellationToken
        );

        return BuildResult(refreshed.Scene, interception.Encounter);
    }

    private static MoveToolResult BuildResult(SceneResult scene, Encounter? encounter) =>
        new(
            scene,
            (encounter as HostileEncounter)?.ToMoveToolSummary(),
            (encounter as GuardEncounter)?.ToMoveToolSummary(),
            (encounter as TheftEncounter)?.ToMoveToolSummary(),
            (encounter as SuspicionEncounter)?.ToMoveToolSummary()
        );
}
