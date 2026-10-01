using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Commands;
using TRPG.Data.ModuleContexts;

namespace TRPG.Application.Creatures.Commands;

public class RestoreStandingPoseCommand
{
    public required Guid CreatureId { get; init; }
}

internal class RestoreStandingPoseCommandHandler(ICreaturesDbContext context)
    : ICommandHandler<RestoreStandingPoseCommand>
{
    public async Task Handle(
        RestoreStandingPoseCommand command,
        CancellationToken cancellationToken = default
    )
    {
        await context
            .Creatures.Where(creature =>
                creature.Id == command.CreatureId
                && creature.StandingX != null
                && creature.StandingY != null
                && creature.StandingAngle != null
            )
            .ExecuteUpdateAsync(
                setters =>
                    setters
                        .SetProperty(creature => creature.X, creature => creature.StandingX!.Value)
                        .SetProperty(creature => creature.Y, creature => creature.StandingY!.Value)
                        .SetProperty(
                            creature => creature.Angle,
                            creature => creature.StandingAngle!.Value
                        )
                        .SetProperty(creature => creature.StandingX, (double?)null)
                        .SetProperty(creature => creature.StandingY, (double?)null)
                        .SetProperty(creature => creature.StandingAngle, (double?)null),
                cancellationToken
            );
    }
}
