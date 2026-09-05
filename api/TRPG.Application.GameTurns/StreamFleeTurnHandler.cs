using System.Text.Json;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Queries;
using TRPG.Application.Creatures.Commands;
using TRPG.Application.Encounters.Commands;
using TRPG.Application.Encounters.Queries;
using TRPG.Application.GameSessions.Queries;
using TRPG.Domain.Models;

namespace TRPG.Application.GameTurns;

internal class StreamFleeTurnHandler(
    GameTurnStreamer streamer,
    ICommandHandler<ResolveFleeCombatCommand, FleeCombatResult?> resolveFleeCombat,
    ICommandHandler<MovePlayerCommand> movePlayer,
    IQueryHandler<GetActiveEncounterQuery, Encounter?> getActiveEncounter,
    ICommandHandler<PublishEncounterStartedCommand> publishEncounterStarted,
    IQueryHandler<GetPlaytimeQuery, TimeSpan> getPlaytime
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
        var result = await resolveFleeCombat.Handle(
            new ResolveFleeCombatCommand
            {
                SessionId = session.SessionId,
                WorldId = session.WorldId,
                PlayerId = session.PlayerId,
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

        var didMove = result.DestinationLocationId != null;

        if (didMove)
        {
            var playtime = await getPlaytime.Handle(
                new GetPlaytimeQuery { SessionId = session.SessionId },
                cancellationToken
            );

            await movePlayer.Handle(
                new MovePlayerCommand
                {
                    PlayerId = session.PlayerId,
                    DestinationLocationId = result.DestinationLocationId!.Value,
                    Playtime = playtime,
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
                },
                cancellationToken
            );
        }

        return new GameTurnPrompt.Narrate(
            BuildNarrationPrompt(result, didMove),
            IncludeTools: false
        );
    }

    internal static string BuildNarrationPrompt(FleeCombatResult result, bool didMove)
    {
        var serializedResult = JsonSerializer.Serialize(
            result.CombatResult,
            TRPG.Application.Common.Serialization.TrpgJsonOptions.Default
        );

        return result.DestinationLocationName != null && didMove
            ? $"The player attempted to flee combat. Result: {serializedResult}. Fleeing broke off the fight and carried the player to {result.DestinationLocationName}. Narrate them breaking away from their attacker, putting distance between themselves and the danger, and arriving there. Do not call any tools."
            : $"The player attempted to flee combat. Result: {serializedResult}. Fleeing only ends the fight — the player has not moved and is still in the same location as the enemy. Narrate them breaking away from the immediate danger (putting distance from their attacker, taking cover, ending the confrontation) without describing them as having left the building, room, or area. Do not call any tools.";
    }
}
