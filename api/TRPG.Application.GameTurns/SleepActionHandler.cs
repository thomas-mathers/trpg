using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Queries;
using TRPG.Application.Creatures.Commands;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.Creatures.Results;
using TRPG.Application.Encounters.Commands;
using TRPG.Application.GameTurns.Commands;
using TRPG.Application.RoomBookings.Commands;
using TRPG.Domain;
using TRPG.Domain.Models;

namespace TRPG.Application.GameTurns;

internal class SleepActionHandler(
    GameActionRunner actionRunner,
    IQueryHandler<GetCreatureByIdQuery, Creature?> getCreatureById,
    ICommandHandler<SleepInRoomCommand, SleepInRoomResult> sleepInRoom,
    ICommandHandler<
        AdvanceCreatureEffectsCommand,
        IReadOnlyCollection<CreatureVitals>
    > advanceCreatureEffects,
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
        if (player!.HasActiveDots)
        {
            return ActionOutcome.Failed(ActionFailure.Afflicted);
        }

        var outcome = await sleepInRoom.Handle(
            new SleepInRoomCommand
            {
                PlayerId = session.PlayerId,
                WorldId = session.WorldId,
                LocationId = player.LocationId,
                Delta = delta,
            },
            cancellationToken
        );

        if (outcome.GameTime is { } gameTime)
        {
            await advanceCreatureEffects.Handle(
                new AdvanceCreatureEffectsCommand
                {
                    WorldId = session.WorldId,
                    CreatureIds = [session.PlayerId],
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
        }

        return outcome.Outcome == SleepOutcome.NotYourRoom
            ? ActionOutcome.Failed(ActionFailure.NotYourRoom)
            : ActionOutcome.Success;
    }
}
