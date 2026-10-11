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
    public async Task<ActionOutcome> Handle(
        GameTurnSession session,
        CancellationToken cancellationToken = default
    )
    {
        var result = await simulation.Request(
            session.WorldId,
            async (poses, ct) =>
            {
                var stood = await standUp.Handle(
                    new StandUpCommand { PlayerId = session.PlayerId },
                    ct
                );
                if (stood == StandUpResult.Success)
                {
                    poses.Forget(session.PlayerId);
                }

                return stood;
            },
            cancellationToken
        );

        return await actionRunner.Run(
            session,
            _ => Task.FromResult(ToOutcome(result)),
            cancellationToken
        );
    }

    private static ActionOutcome ToOutcome(StandUpResult result) =>
        result switch
        {
            StandUpResult.Success => ActionOutcome.Success,
            StandUpResult.PlayerNotSitting => ActionOutcome.Failed(ActionFailure.NotSitting),
            _ => throw new InvalidOperationException($"Unhandled stand result {result}."),
        };
}
