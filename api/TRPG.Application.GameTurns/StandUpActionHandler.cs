using TRPG.Application.Common.Commands;
using TRPG.Application.GameTurns.Commands;
using TRPG.Application.WorldSimulation.Poses;

namespace TRPG.Application.GameTurns;

internal class StandUpActionHandler(
    GameActionRunner actionRunner,
    ICommandHandler<StandUpCommand, StandUpResult> standUp
)
{
    public Task<ActionOutcome> Handle(
        GameTurnSession session,
        CancellationToken cancellationToken = default
    ) =>
        actionRunner.Run(
            session,
            (poses, token) => StandUp(session, poses, token),
            cancellationToken
        );

    private async Task<ActionOutcome> StandUp(
        GameTurnSession session,
        LivePlayerPoses poses,
        CancellationToken cancellationToken
    )
    {
        var result = await standUp.Handle(
            new StandUpCommand { PlayerId = session.PlayerId },
            cancellationToken
        );
        if (result == StandUpResult.Success)
        {
            poses.Forget(session.PlayerId);
        }

        return ToOutcome(result);
    }

    private static ActionOutcome ToOutcome(StandUpResult result) =>
        result switch
        {
            StandUpResult.Success => ActionOutcome.Success,
            StandUpResult.PlayerNotSitting => ActionOutcome.Failed(ActionFailure.NotSitting),
            _ => throw new InvalidOperationException($"Unhandled stand result {result}."),
        };
}
