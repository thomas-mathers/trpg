using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Validation;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Creatures.Commands;

public class FlushPlayerPoseCommand
{
    [NotEmptyGuid]
    public required Guid PlayerId { get; init; }
}

internal class FlushPlayerPoseCommandHandler(ICreaturesDbContext context, PlayerPoseStore poseStore)
    : ICommandHandler<FlushPlayerPoseCommand>
{
    public async Task Handle(
        FlushPlayerPoseCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var pose = poseStore.TakeDirty(command.PlayerId);
        if (pose is null)
        {
            return;
        }

        var x = pose.X;
        var y = pose.Y;
        var angle = pose.Angle;

        await context
            .Creatures.Where(creature =>
                creature.Id == command.PlayerId
                && creature.LocationId == pose.LocationId
                && creature.Posture == CreaturePosture.Standing
            )
            .ExecuteUpdateAsync(
                setters =>
                    setters
                        .SetProperty(creature => creature.X, x)
                        .SetProperty(creature => creature.Y, y)
                        .SetProperty(creature => creature.Angle, angle),
                cancellationToken
            );
    }
}
