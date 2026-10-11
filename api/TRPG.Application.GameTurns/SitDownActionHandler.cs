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
    public async Task<ActionOutcome> Handle(
        GameTurnSession session,
        Guid seatId,
        CancellationToken cancellationToken = default
    )
    {
        var result = await simulation.Request(
            session.WorldId,
            async (poses, ct) =>
            {
                var sat = await sitDown.Handle(
                    new SitDownCommand
                    {
                        PlayerId = session.PlayerId,
                        SeatId = seatId,
                        LivePose = poses.Find(session.PlayerId),
                    },
                    ct
                );
                if (sat == SitDownResult.Success)
                {
                    poses.Forget(session.PlayerId);
                }

                return sat;
            },
            cancellationToken
        );

        return await actionRunner.Run(
            session,
            _ => Task.FromResult(ToOutcome(result)),
            cancellationToken
        );
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
