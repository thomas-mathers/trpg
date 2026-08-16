using System.Text.Json;
using TRPG.Application.Combat;
using TRPG.Application.Combat.Commands;
using TRPG.Application.Combat.Results;
using TRPG.Application.Common.Commands;
using TRPG.Application.GameSessions;

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
            $"The player attempted to flee combat. Result: {JsonSerializer.Serialize(result, TRPG.Application.Common.Serialization.TrpgJsonOptions.Default)}. Narrate the escape attempt vividly based on this result. Do not call any tools.",
            IncludeTools: false
        );
    }
}
