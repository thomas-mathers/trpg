using TRPG.Application.Combat;
using TRPG.Application.Combat.Commands;
using TRPG.Application.Combat.Queries;
using TRPG.Application.Common.Events;
using TRPG.Application.Creatures.Commands;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.GameSessions;
using TRPG.Application.Scenes;
using TRPG.Application.Scenes.Commands;
using TRPG.Application.Scenes.Queries;
using TRPG.Contracts.Combat.Requests;

namespace TRPG.Application.GameTurns;

internal class ResolveCombatActionHandler(
    ApplyPassiveRegenCommandHandler applyPassiveRegen,
    GetActiveFightCombatantsQueryHandler getCombatants,
    CombatEngine combatEngine,
    ResolveCombatRoundCommandHandler resolveCombatRound,
    GetCreatureByIdQueryHandler getCreatureById,
    EnsureLocationCatchUpCommandHandler ensureLocationCatchUp,
    GetSceneQueryHandler getScene,
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

        // GetSceneWithCatchUpQuery also pushes a SceneUpdatedEvent when a catch-up ran, so its
        // pieces are composed directly here to avoid a duplicate push alongside this one.
        var player = await getCreatureById.Handle(
            new GetCreatureByIdQuery { Id = session.PlayerId },
            cancellationToken
        );

        if (player is not null)
        {
            var catchUp = await ensureLocationCatchUp.Handle(
                new EnsureLocationCatchUpCommand
                {
                    WorldId = session.WorldId,
                    LocationId = player.LocationId,
                    SessionId = session.SessionId,
                },
                cancellationToken
            );
            var scene = await getScene.Handle(
                new GetSceneQuery
                {
                    WorldId = session.WorldId,
                    PlayerId = session.PlayerId,
                    CurrentDate = catchUp.CurrentDate,
                },
                cancellationToken
            );
            gameEvents.Enqueue(
                new SceneUpdatedEvent(
                    SceneSnapshotMapper.ToSnapshot(scene),
                    SceneUpdateReason.Synced
                )
            );
        }
    }
}
