using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Queries;
using TRPG.Application.Creatures.Commands;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.Creatures.Results;
using TRPG.Application.Encounters.Commands;
using TRPG.Application.GameTurns.Commands;
using TRPG.Application.Worlds.Commands;
using TRPG.Domain;
using TRPG.Domain.Models;

namespace TRPG.Application.GameTurns;

internal class WaitActionHandler(
    GameActionRunner actionRunner,
    ICommandHandler<
        AdvanceCreatureEffectsCommand,
        IReadOnlyCollection<CreatureVitals>
    > advanceCreatureEffects,
    ICommandHandler<
        ApplyPassiveRegenCommand,
        IReadOnlyDictionary<Guid, Creature>
    > applyPassiveRegen,
    ICommandHandler<AdvanceTimeCommand, GameInstant> advanceTime,
    IQueryHandler<GetCreatureByIdQuery, Creature?> getCreatureById,
    ICommandHandler<RefreshSceneCommand, RefreshSceneResult> refreshScene
)
{
    public Task<ActionOutcome> Handle(
        GameTurnSession session,
        int hours,
        int minutes,
        CancellationToken cancellationToken = default
    ) => actionRunner.Run(session, ct => Resolve(session, hours, minutes, ct), cancellationToken);

    private async Task<ActionOutcome> Resolve(
        GameTurnSession session,
        int hours,
        int minutes,
        CancellationToken cancellationToken
    )
    {
        var delta = TimeSpan.FromHours(hours) + TimeSpan.FromMinutes(minutes);
        if (delta <= TimeSpan.Zero)
        {
            return ActionOutcome.Failed(ActionFailure.InvalidDuration);
        }
        if (delta > TimeSpan.FromHours(24))
        {
            return ActionOutcome.Failed(ActionFailure.InvalidDuration);
        }

        var player = await getCreatureById.Handle(
            new GetCreatureByIdQuery { Id = session.PlayerId },
            cancellationToken
        );
        if (player?.Posture != CreaturePosture.Sitting)
        {
            return ActionOutcome.Failed(ActionFailure.NotSitting);
        }
        if (player.HasActiveDots)
        {
            return ActionOutcome.Failed(ActionFailure.Afflicted);
        }

        var gameTime = await advanceTime.Handle(
            new AdvanceTimeCommand { WorldId = session.WorldId, Delta = delta },
            cancellationToken
        );

        await advanceCreatureEffects.Handle(
            new AdvanceCreatureEffectsCommand
            {
                WorldId = session.WorldId,
                CreatureIds = [session.PlayerId],
                GameTime = gameTime,
            },
            cancellationToken
        );
        await applyPassiveRegen.Handle(
            new ApplyPassiveRegenCommand { GameTime = gameTime, CreatureIds = [session.PlayerId] },
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

        return ActionOutcome.TimeAdvanced(gameTime);
    }
}
