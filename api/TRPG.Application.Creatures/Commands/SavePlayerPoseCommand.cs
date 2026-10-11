using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Validation;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Creatures.Commands;

public class SavePlayerPoseCommand
{
    [NotEmptyGuid]
    public required Guid PlayerId { get; init; }

    [NotEmptyGuid]
    public required Guid LocationId { get; init; }

    public required double X { get; init; }
    public required double Y { get; init; }
    public required double Angle { get; init; }
}

internal class SavePlayerPoseCommandHandler(ICreaturesDbContext context)
    : ICommandHandler<SavePlayerPoseCommand>
{
    public async Task Handle(
        SavePlayerPoseCommand command,
        CancellationToken cancellationToken = default
    )
    {
        await context
            .Creatures.Where(creature =>
                creature.Id == command.PlayerId
                && creature.LocationId == command.LocationId
                && creature.Posture == CreaturePosture.Standing
            )
            .ExecuteUpdateAsync(
                setters =>
                    setters
                        .SetProperty(creature => creature.X, command.X)
                        .SetProperty(creature => creature.Y, command.Y)
                        .SetProperty(creature => creature.Angle, command.Angle),
                cancellationToken
            );
    }
}
