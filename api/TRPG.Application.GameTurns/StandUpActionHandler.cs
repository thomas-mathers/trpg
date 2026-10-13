using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Queries;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.GameTurns.Commands;
using TRPG.Application.WorldSimulation.Poses;

namespace TRPG.Application.GameTurns;

internal class StandUpActionHandler(
    GameActionRunner actionRunner,
    SeatingAnnouncer seatingAnnouncer,
    ICommandHandler<StandUpCommand, StandUpResult> standUp,
    IQueryHandler<GetSettledPlayerPoseQuery, SettledPlayerPose?> getSettledPose
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
            await SeedStandingPose(session.PlayerId, poses, cancellationToken);
            await seatingAnnouncer.Announce(session, null, cancellationToken);
        }

        return ToOutcome(result);
    }

    private async Task SeedStandingPose(
        Guid playerId,
        LivePlayerPoses poses,
        CancellationToken cancellationToken
    )
    {
        var settled = await getSettledPose.Handle(
            new GetSettledPlayerPoseQuery { PlayerId = playerId },
            cancellationToken
        );
        if (settled is null)
        {
            poses.Forget(playerId);
            return;
        }

        poses.Seed(playerId, settled.ToIdlePose());
    }

    private static ActionOutcome ToOutcome(StandUpResult result) =>
        result switch
        {
            StandUpResult.Success => ActionOutcome.Success,
            StandUpResult.PlayerNotSitting => ActionOutcome.Failed(ActionFailure.NotSitting),
            _ => throw new InvalidOperationException($"Unhandled stand result {result}."),
        };
}
