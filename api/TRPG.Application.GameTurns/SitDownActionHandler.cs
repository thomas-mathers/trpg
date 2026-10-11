using TRPG.Application.Common.Commands;
using TRPG.Application.GameTurns.Commands;
using TRPG.Application.WorldSimulation;

namespace TRPG.Application.GameTurns;

internal class SitDownActionHandler(
    GameActionRunner actionRunner,
    ICommandHandler<SitDownCommand, SitDownResult> sitDown,
    WorldSimulationCoordinator simulation
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
                if (result == SitDownResult.Success)
                {
                    simulation.Post(session.WorldId, new ForgetPlayerPose(session.PlayerId));
                }

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
