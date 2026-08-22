using System.Text.Json;
using TRPG.Application.Combat.Commands;
using TRPG.Application.Combat.Results;
using TRPG.Application.Common.Commands;

namespace TRPG.Application.GameTurns;

internal class StreamFleeTurnHandler(
    GameTurnStreamer streamer,
    ICommandHandler<ResolveFleeCombatCommand, CombatResult?> resolveFleeCombat
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

        return new GameTurnPrompt.Narrate(
            $"The player attempted to flee combat. Result: {JsonSerializer.Serialize(result, TRPG.Application.Common.Serialization.TrpgJsonOptions.Default)}. Fleeing only ends the fight — the player has not moved and is still in the same location as the enemy. Narrate them breaking away from the immediate danger (putting distance from their attacker, taking cover, ending the confrontation) without describing them as having left the building, room, or area. Do not call any tools.",
            IncludeTools: false
        );
    }
}
