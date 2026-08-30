using System.Text.Json;
using TRPG.Application.Combat;
using TRPG.Application.Combat.Commands;
using TRPG.Application.Common.Commands;
using TRPG.Application.Scenes.Commands;
using TRPG.Domain.Models;

namespace TRPG.Application.GameTurns;

internal record CombatConclusionFact(
    CombatOutcome Outcome,
    IReadOnlyCollection<string> OpponentNames
);

internal class StreamCombatActionTurnHandler(
    GameTurnStreamer streamer,
    ICommandHandler<ResolvePlayerCombatActionCommand, PlayerCombatActionResult> resolveCombatAction,
    ICommandHandler<RefreshSceneCommand, RefreshSceneResult> refreshScene
)
{
    public IAsyncEnumerable<string> Handle(
        GameTurnSession session,
        PlayerCombatAction action,
        CancellationToken cancellationToken = default
    ) => streamer.StreamTurn(session, ct => ResolveTurn(session, action, ct), cancellationToken);

    private async Task<GameTurnPrompt> ResolveTurn(
        GameTurnSession session,
        PlayerCombatAction action,
        CancellationToken cancellationToken
    )
    {
        var result = await resolveCombatAction.Handle(
            new ResolvePlayerCombatActionCommand
            {
                SessionId = session.SessionId,
                WorldId = session.WorldId,
                PlayerId = session.PlayerId,
                Action = action,
            },
            cancellationToken
        );

        await refreshScene.Handle(
            new RefreshSceneCommand
            {
                WorldId = session.WorldId,
                PlayerId = session.PlayerId,
                SessionId = session.SessionId,
            },
            cancellationToken
        );

        if (result.CombatResult.Outcome == CombatOutcome.Ongoing)
        {
            return new GameTurnPrompt.None();
        }

        var fact = new CombatConclusionFact(result.CombatResult.Outcome, result.OpponentNames);

        return new GameTurnPrompt.Narrate(BuildNarrationPrompt(fact), IncludeTools: false);
    }

    internal static string BuildNarrationPrompt(CombatConclusionFact fact)
    {
        var json = JsonSerializer.Serialize(fact, Common.Serialization.TrpgJsonOptions.Default);

        return fact.Outcome switch
        {
            CombatOutcome.Victory => $"""
                The fight has concluded. Result: {json}.
                Every listed opponent is dead — do not depict any of them as alive, conscious, or
                speaking afterward. Narrate the fight's conclusion vividly: the killing blow and its
                immediate aftermath. Do not call any tools.
                """,
            CombatOutcome.Defeat => $"""
                The fight has concluded. Result: {json}.
                The player has been struck down and lost consciousness; the opponent(s) survive.
                Narrate only up to the moment the player blacks out — what happens next is handled
                separately, so do not narrate anything after that. Do not call any tools.
                """,
            _ => throw new ArgumentOutOfRangeException(nameof(fact)),
        };
    }
}
