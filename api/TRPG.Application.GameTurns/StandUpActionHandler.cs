using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Queries;
using TRPG.Application.Creatures;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.GameTurns.Commands;
using TRPG.Application.WorldSimulation.Poses;
using TRPG.Domain.Models;

namespace TRPG.Application.GameTurns;

internal class StandUpActionHandler(
    GameActionRunner actionRunner,
    SeatingAnnouncer seatingAnnouncer,
    ICommandHandler<StandUpCommand, StandUpResult> standUp,
    IQueryHandler<GetCreatureByIdQuery, Creature?> getCreatureById
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
        var player = await getCreatureById.Handle(
            new GetCreatureByIdQuery { Id = playerId },
            cancellationToken
        );
        if (player is null)
        {
            poses.Forget(playerId);
            return;
        }

        poses.Seed(playerId, PlayerPose.Idle(player));
    }

    private static ActionOutcome ToOutcome(StandUpResult result) =>
        result switch
        {
            StandUpResult.Success => ActionOutcome.Success,
            StandUpResult.PlayerNotSitting => ActionOutcome.Failed(ActionFailure.NotSitting),
            _ => throw new InvalidOperationException($"Unhandled stand result {result}."),
        };
}
