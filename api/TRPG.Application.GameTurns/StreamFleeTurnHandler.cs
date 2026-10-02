using System.Text.Json;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Queries;
using TRPG.Application.Creatures.Commands;
using TRPG.Application.Encounters.Commands;
using TRPG.Application.Encounters.Queries;
using TRPG.Application.GameSessions.Queries;
using TRPG.Domain;
using TRPG.Domain.Models;

namespace TRPG.Application.GameTurns;

internal class StreamFleeTurnHandler(
    GameTurnStreamer streamer,
    ICommandHandler<ResolveFleeCombatCommand, FleeCombatResult?> resolveFleeCombat,
    ICommandHandler<MovePlayerCommand> movePlayer,
    IQueryHandler<GetActiveEncounterQuery, Encounter?> getActiveEncounter,
    ICommandHandler<PublishEncounterStartedCommand> publishEncounterStarted,
    IQueryHandler<GetGameTimeQuery, GameInstant> getGameTime
)
{
    public IAsyncEnumerable<string> Handle(
        GameTurnSession session,
        CancellationToken cancellationToken = default
    ) => streamer.StreamTurn(session, ct => ResolveTurn(session, ct), cancellationToken);

    private async Task<GameTurnPrompt> ResolveTurn(
        GameTurnSession session,
        CancellationToken cancellationToken
    )
    {
        var gameTime = await getGameTime.Handle(
            new GetGameTimeQuery { SessionId = session.SessionId },
            cancellationToken
        );

        var result = await resolveFleeCombat.Handle(
            new ResolveFleeCombatCommand
            {
                SessionId = session.SessionId,
                WorldId = session.WorldId,
                PlayerId = session.PlayerId,
                GameTime = gameTime,
            },
            cancellationToken
        );

        if (result == null)
        {
            return new GameTurnPrompt.Reply("There's no fight to flee from right now.");
        }

        if (result.CombatResult.Outcome != CombatOutcome.Fled)
        {
            return new GameTurnPrompt.None();
        }

        if (result.DestinationLocationId is not { } destinationLocationId)
        {
            return new GameTurnPrompt.Narrate(BuildNarrationPrompt(result), IncludeTools: false);
        }

        await movePlayer.Handle(
            new MovePlayerCommand
            {
                PlayerId = session.PlayerId,
                DestinationLocationId = destinationLocationId,
                GameTime = gameTime,
            },
            cancellationToken
        );

        var startedEncounter = await getActiveEncounter.Handle(
            new GetActiveEncounterQuery { PlayerId = session.PlayerId },
            cancellationToken
        );

        await publishEncounterStarted.Handle(
            new PublishEncounterStartedCommand
            {
                PlayerId = session.PlayerId,
                Encounter = startedEncounter,
                GameTime = gameTime,
            },
            cancellationToken
        );

        return new GameTurnPrompt.None();
    }

    internal static string BuildNarrationPrompt(FleeCombatResult result)
    {
        var serializedResult = JsonSerializer.Serialize(
            result.CombatResult,
            TRPG.Application.Common.Serialization.TrpgJsonOptions.Default
        );

        return $"The player attempted to flee combat. Result: {serializedResult}. Fleeing only ends the fight — the player has not moved and is still in the same location as the enemy. Narrate them breaking away from the immediate danger (putting distance from their attacker, taking cover, ending the confrontation) without describing them as having left the building, room, or area. Do not call any tools.";
    }
}
