using TRPG.Application.Common.Commands;
using TRPG.Application.GameTurns.Commands;

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
            async ct =>
            {
                var result = await standUp.Handle(
                    new StandUpCommand { PlayerId = session.PlayerId },
                    ct
                );
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
