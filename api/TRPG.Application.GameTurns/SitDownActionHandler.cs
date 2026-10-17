using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Queries;
using TRPG.Application.Encounters.Queries;
using TRPG.Application.GameTurns.Commands;
using TRPG.Application.WorldSimulation.Poses;

namespace TRPG.Application.GameTurns;

internal class SitDownActionHandler(
    GameActionRunner actionRunner,
    SeatingAnnouncer seatingAnnouncer,
    IQueryHandler<HasActiveFightQuery, bool> hasActiveFight,
    ICommandHandler<SitDownCommand, SitDownResult> sitDown
)
{
    public Task<ActionOutcome> Handle(
        GameTurnSession session,
        Guid seatId,
        CancellationToken cancellationToken = default
    ) =>
        actionRunner.Run(
            session,
            (poses, token) => SitDown(session, seatId, poses, token),
            cancellationToken
        );

    private async Task<ActionOutcome> SitDown(
        GameTurnSession session,
        Guid seatId,
        LivePlayerPoses poses,
        CancellationToken cancellationToken
    )
    {
        var fightHeld = await hasActiveFight.Handle(
            new HasActiveFightQuery { PlayerId = session.PlayerId },
            cancellationToken
        );
        if (fightHeld)
        {
            return ActionOutcome.Failed(ActionFailure.EncounterActive);
        }

        var result = await sitDown.Handle(
            new SitDownCommand
            {
                PlayerId = session.PlayerId,
                SeatId = seatId,
                LivePose = poses.Find(session.PlayerId),
            },
            cancellationToken
        );
        if (result == SitDownResult.Success)
        {
            poses.Forget(session.PlayerId);
            await seatingAnnouncer.Announce(session, seatId, cancellationToken);
        }

        return ToOutcome(result);
    }

    private static ActionOutcome ToOutcome(SitDownResult result) =>
        result switch
        {
            SitDownResult.Success => ActionOutcome.Success,
            SitDownResult.SeatOccupied => ActionOutcome.Failed(ActionFailure.SeatOccupied),
            SitDownResult.SeatNotNearby => ActionOutcome.Failed(ActionFailure.SeatNotNearby),
            SitDownResult.PlayerNotIdle => ActionOutcome.Failed(ActionFailure.PlayerNotIdle),
            _ => throw new InvalidOperationException($"Unhandled sit result {result}."),
        };
}
