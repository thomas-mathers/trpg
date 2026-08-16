using TRPG.Application.Combat;
using TRPG.Application.Combat.Commands;
using TRPG.Application.Combat.Queries;
using TRPG.Application.Common.Events;
using TRPG.Application.Common.Handling;
using TRPG.Application.Creatures.Commands;
using TRPG.Application.GameSessions;
using TRPG.Application.GameTurns.Events;
using TRPG.Application.Scenes;
using TRPG.Application.Scenes.Commands;
using TRPG.Contracts.Combat.Requests;
using TRPG.Data.Models;

namespace TRPG.Application.GameTurns;

internal class ResolveCombatActionHandler(
    ICommandHandler<
        ApplyPassiveRegenCommand,
        IReadOnlyDictionary<Guid, Creature>
    > applyPassiveRegen,
    IQueryHandler<GetActiveFightCombatantsQuery, IReadOnlyList<Combatant>> getCombatants,
    CombatEngine combatEngine,
    ICommandHandler<ResolveCombatRoundCommand, CombatResult> resolveCombatRound,
    ICommandHandler<RefreshSceneCommand, RefreshSceneResult> refreshScene,
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
        gameEvents.Enqueue(new SceneUpdatedEvent(refreshed.Scene));
    }
}
