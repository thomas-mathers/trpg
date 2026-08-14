using TRPG.Application.Combat;
using TRPG.Application.Combat.Commands;
using TRPG.Application.Combat.Queries;
using TRPG.Application.Common.Events;
using TRPG.Application.Creatures.Commands;
using TRPG.Application.GameSessions;
using TRPG.Application.Scenes;
using TRPG.Application.Scenes.Commands;
using TRPG.Contracts.Combat.Requests;

namespace TRPG.Application.GameTurns;

internal class ResolveCombatActionHandler(
    ApplyPassiveRegenCommandHandler applyPassiveRegen,
    GetActiveFightCombatantsQueryHandler getCombatants,
    CombatEngine combatEngine,
    ResolveCombatRoundCommandHandler resolveCombatRound,
    RefreshSceneCommandHandler refreshScene,
    IGameClientEventSink gameEvents
)
{
    public async Task Handle(
        GameSessionIdentity session,
        PlayerCombatAction action,
        CancellationToken cancellationToken = default
    )
    {
        await applyPassiveRegen.Handle(
            new ApplyPassiveRegenCommand
            {
                SessionId = session.SessionId,
                CreatureIds = [session.PlayerId],
            },
            cancellationToken
        );

        var combatants = await getCombatants.Handle(
            new GetActiveFightCombatantsQuery { PlayerId = session.PlayerId },
            cancellationToken
        );

        if (combatants.Count == 0)
        {
            throw new InvalidOperationException("There's no fight to act in right now.");
        }

        var resolverResult = new PlayerCombatActionResolver(combatants).Resolve(action);

        if (resolverResult.ErrorMessage is not null)
        {
            throw new InvalidOperationException(resolverResult.ErrorMessage);
        }

        var state = combatEngine.ProcessRound(combatants, resolverResult.Result!);

        await resolveCombatRound.Handle(
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

        var refreshed = await refreshScene.Handle(
            new RefreshSceneCommand
            {
                WorldId = session.WorldId,
                PlayerId = session.PlayerId,
                SessionId = session.SessionId,
            },
            cancellationToken
        );
        gameEvents.Enqueue(
            new SceneUpdatedEvent(
                SceneSnapshotMapper.ToSnapshot(refreshed.Scene),
                SceneUpdateReason.Synced
            )
        );
    }
}
