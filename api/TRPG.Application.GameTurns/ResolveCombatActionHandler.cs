using TRPG.Application.Combat;
using TRPG.Application.Combat.Commands;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Events;
using TRPG.Application.GameTurns.Events;
using TRPG.Application.Scenes.Commands;

namespace TRPG.Application.GameTurns;

internal class ResolveCombatActionHandler(
    ICommandHandler<ResolvePlayerCombatActionCommand> resolveCombatAction,
    ICommandHandler<RefreshSceneCommand, RefreshSceneResult> refreshScene,
    IGameClientEventSink gameEvents
)
{
    public async Task Handle(
        GameTurnSession session,
        PlayerCombatAction action,
        CancellationToken cancellationToken = default
    )
    {
        await resolveCombatAction.Handle(
            new ResolvePlayerCombatActionCommand
            {
                SessionId = session.SessionId,
                WorldId = session.WorldId,
                PlayerId = session.PlayerId,
                Action = action,
            },
            cancellationToken
        );

        var refreshed = await refreshScene.Handle(
            new RefreshSceneCommand
            {
                WorldId = session.WorldId,
                PlayerId = session.PlayerId,
                SessionId = session.SessionId,
            },
            cancellationToken
        );
        gameEvents.Enqueue(new SceneUpdatedEvent(refreshed.Scene));
    }
}
