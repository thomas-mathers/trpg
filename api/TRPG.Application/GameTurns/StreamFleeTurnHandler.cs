using System.Text.Json;
using TRPG.Application.Combat;
using TRPG.Application.Combat.Commands;
using TRPG.Application.Combat.Queries;
using TRPG.Application.Common.Tools;
using TRPG.Application.GameSessions;

namespace TRPG.Application.GameTurns;

internal class StreamFleeTurnHandler(
    GameTurnStreamer streamer,
    GetActiveFightCombatantsQueryHandler getCombatants,
    CombatEngine combatEngine,
    ResolveCombatRoundCommandHandler resolveCombatRound
)
{
    public IAsyncEnumerable<string> Handle(
        GameSessionIdentity session,
        CancellationToken cancellationToken = default
    ) => streamer.StreamTurn(session, ct => ResolveTurn(session, ct), cancellationToken);

    private async Task<GameTurnPrompt> ResolveTurn(
        GameSessionIdentity session,
        CancellationToken cancellationToken
    )
    {
        var combatants = await getCombatants.Handle(
            new GetActiveFightCombatantsQuery { PlayerId = session.PlayerId },
            cancellationToken
        );

        if (combatants.Count == 0)
        {
            return new GameTurnPrompt.Reply("There's no fight to flee from right now.");
        }

        var state = combatEngine.ResolveFlee(combatants);

        var result = await resolveCombatRound.Handle(
            new ResolveCombatRoundCommand
            {
                SessionId = session.SessionId,
                WorldId = session.WorldId,
                PlayerId = session.PlayerId,
                Combatants = combatants,
                State = state,
            },
            cancellationToken
        );

        return new GameTurnPrompt.Narrate(
            $"The player attempted to flee combat. Result: {JsonSerializer.Serialize(result, ToolJsonOptions.Options)}. Narrate the escape attempt vividly based on this result. Do not call any tools.",
            IncludeTools: false
        );
    }
}
