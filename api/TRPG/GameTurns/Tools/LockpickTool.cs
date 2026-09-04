using System.ComponentModel;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Queries;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.Encounters.Commands;
using TRPG.Application.Encounters.Queries;
using TRPG.Application.GameTurns;
using TRPG.Application.Worlds.Queries;
using TRPG.Domain.Models;
using TRPG.GameTurns.Mappers;
using TRPG.Tools;

namespace TRPG.GameTurns.Tools;

internal record LockpickToolResult(
    bool Opened,
    MoveToolGuardEncounter? GuardEncounter,
    MoveToolHostileEncounter? HostileEncounter
);

internal class LockpickTool(
    GameTurnContext turnContext,
    IQueryHandler<GetActiveEncounterQuery, Encounter?> getActiveEncounter,
    IQueryHandler<GetCreatureByIdQuery, Creature?> getCreatureById,
    IQueryHandler<GetExitByDestinationNameQuery, ExitMatch> getExitByDestinationName,
    ICommandHandler<AttemptLockpickCommand, AttemptLockpickResult> attemptLockpick
) : IGameTool
{
    public Delegate Invoke => InvokeAsync;

    [DisplayName("pick_lock")]
    [Description(
        "Attempts to pick the lock on a nearby locked door by exact name — a building's front door, or an interior door within a building the player is already inside. Picking a lock is a crime if witnessed, with real consequences (fines, jail, hostile confrontation). Call this ONLY after the player explicitly says they want to pick, force, or break into the lock or door — never call it automatically as a fallback when a move fails because a door is locked, and never call it merely because a door is known or described as locked. A locked door encountered during ordinary movement should just be narrated as locked; the player must explicitly choose to attempt lockpicking as its own action. Opening the lock does NOT move the player through the door — narrate only the lock itself (it opening or resisting); never describe the player entering, stepping through, or being inside as part of this result, even if Opened is true. The player is still standing where they were and must issue a separate move to actually go through. The name must be copied verbatim from the most recent look or move result — never invented, guessed, or paraphrased."
    )]
    private async Task<object?> InvokeAsync(
        [Description(
            "The exact Name of a nearby building, or the exact DestinationRoomName of an exit, copied verbatim from the most recent look or move result."
        )]
            string destinationName,
        CancellationToken cancellationToken
    )
    {
        var activeEncounter = await getActiveEncounter.Handle(
            new GetActiveEncounterQuery { PlayerId = turnContext.PlayerId },
            cancellationToken
        );
        if (activeEncounter != null)
        {
            return new ToolError("You can't do that while an encounter is active.");
        }

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
        if (!exitMatch.Matched)
        {
            return new ToolError($"There's no door to '{destinationName}' here.");
        }

        var result = await attemptLockpick.Handle(
            new AttemptLockpickCommand
            {
                PlayerId = turnContext.PlayerId,
                WorldId = turnContext.WorldId,
                ConnectorId = exitMatch.ConnectorId!.Value,
                DestinationLocationId = exitMatch.DestinationLocationId!.Value,
            },
            cancellationToken
        );

        if (result.Outcome == LockpickAttemptOutcome.NothingToPick)
        {
            return new ToolError($"The door to '{destinationName}' isn't locked.");
        }

        return new LockpickToolResult(
            result.Outcome == LockpickAttemptOutcome.Opened,
            (result.Encounter as GuardEncounter)?.ToMoveToolSummary(),
            (result.Encounter as HostileEncounter)?.ToMoveToolSummary()
        );
    }
}
