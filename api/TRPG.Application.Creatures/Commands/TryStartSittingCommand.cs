using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Commands;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Creatures.Commands;

public class TryStartSittingCommand
{
    public required Guid CreatureId { get; init; }
    public required Guid LocationId { get; init; }
    public required double X { get; init; }
    public required double Y { get; init; }
    public required double Angle { get; init; }
}

internal class TryStartSittingCommandHandler(ICreaturesDbContext context)
    : ICommandHandler<TryStartSittingCommand, bool>
{
    public async Task<bool> Handle(
        TryStartSittingCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var updated = await context
            .Creatures.Where(creature =>
                creature.Id == command.CreatureId
                && creature.LocationId == command.LocationId
                && creature.Condition == CreatureCondition.Awake
                && creature.Movement == CreatureMovement.Stationary
                && creature.Posture == CreaturePosture.Standing
            )
            .ExecuteUpdateAsync(
                setters =>
                    setters
                        .SetProperty(creature => creature.Posture, CreaturePosture.Sitting)
                        .SetProperty(creature => creature.StandingX, creature => creature.X)
                        .SetProperty(creature => creature.StandingY, creature => creature.Y)
                        .SetProperty(creature => creature.StandingAngle, creature => creature.Angle)
                        .SetProperty(creature => creature.X, command.X)
                        .SetProperty(creature => creature.Y, command.Y)
                        .SetProperty(creature => creature.Angle, command.Angle),
                cancellationToken
            );

        return updated == 1;
    }
}
