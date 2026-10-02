using TRPG.Application.Combat;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Queries;
using TRPG.Application.Encounters.Commands;
using TRPG.Application.GameSessions.Queries;
using TRPG.Application.GameTurns.Commands;
using TRPG.Domain;
using TRPG.Domain.Models;

namespace TRPG.Application.GameTurns;

internal class CombatActionHandler(
    GameActionRunner actionRunner,
    ICommandHandler<ResolvePlayerCombatActionCommand, PlayerCombatActionResult> resolveCombatAction,
    ICommandHandler<RefreshSceneCommand, RefreshSceneResult> refreshScene,
    IQueryHandler<GetGameTimeQuery, GameInstant> getGameTime
)
{
    public Task<ActionOutcome> Handle(
        GameTurnSession session,
        PlayerCombatAction action,
        CancellationToken cancellationToken = default
    ) => actionRunner.Run(session, ct => Resolve(session, action, ct), cancellationToken);

    private async Task<ActionOutcome> Resolve(
        GameTurnSession session,
        PlayerCombatAction action,
        CancellationToken cancellationToken
    )
    {
        var gameTime = await getGameTime.Handle(
            new GetGameTimeQuery { SessionId = session.SessionId },
            cancellationToken
        );

        await resolveCombatAction.Handle(
            new ResolvePlayerCombatActionCommand
            {
                SessionId = session.SessionId,
                WorldId = session.WorldId,
                PlayerId = session.PlayerId,
                Action = action,
                GameTime = gameTime,
            },
            cancellationToken
        );

        await refreshScene.Handle(
            new RefreshSceneCommand
            {
                WorldId = session.WorldId,
                PlayerId = session.PlayerId,
                GameTime = gameTime,
            },
            cancellationToken
        );

        return ActionOutcome.Success;
    }
}
