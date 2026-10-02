using TRPG.Application.Common.Commands;
using TRPG.Application.GameTurns.Commands;

namespace TRPG.Application.GameTurns;

internal class SitDownActionHandler(
    GameActionRunner actionRunner,
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
            async ct =>
            {
                var result = await sitDown.Handle(
                    new SitDownCommand { PlayerId = session.PlayerId, SeatId = seatId },
                    ct
                );
                return result switch
                {
                    SitDownResult.Success => ActionOutcome.Success,
                    SitDownResult.SeatOccupied => ActionOutcome.Failed(ActionFailure.SeatOccupied),
                    SitDownResult.SeatNotNearby => ActionOutcome.Failed(
                        ActionFailure.SeatNotNearby
                    ),
                    SitDownResult.PlayerNotIdle => ActionOutcome.Failed(
                        ActionFailure.PlayerNotIdle
                    ),
                    _ => throw new InvalidOperationException($"Unhandled sit result {result}."),
                };
            },
            cancellationToken
        );
}
