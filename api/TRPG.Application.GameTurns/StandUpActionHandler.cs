using TRPG.Application.Common.Commands;
using TRPG.Application.GameTurns.Commands;
using TRPG.Application.WorldSimulation;

namespace TRPG.Application.GameTurns;

internal class StandUpActionHandler(
    GameActionRunner actionRunner,
    ICommandHandler<StandUpCommand, StandUpResult> standUp,
    WorldSimulationCoordinator simulation
)
{
    public Task<ActionOutcome> Handle(
        GameTurnSession session,
        CancellationToken cancellationToken = default
    ) =>
        actionRunner.Run(
            session,
            async ct =>
            {
                var result = await standUp.Handle(
                    new StandUpCommand { PlayerId = session.PlayerId },
                    ct
                );
                if (result == StandUpResult.Success)
                {
                    simulation.Post(session.WorldId, new ForgetPlayerPose(session.PlayerId));
                }

                return result switch
                {
                    StandUpResult.Success => ActionOutcome.Success,
                    StandUpResult.PlayerNotSitting => ActionOutcome.Failed(
                        ActionFailure.NotSitting
                    ),
                    _ => throw new InvalidOperationException($"Unhandled stand result {result}."),
                };
            },
            cancellationToken
        );
}
