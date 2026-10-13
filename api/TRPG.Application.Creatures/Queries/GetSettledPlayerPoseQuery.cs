using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Navigation;
using TRPG.Application.Common.Queries;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Creatures.Queries;

public class GetSettledPlayerPoseQuery
{
    public required Guid PlayerId { get; init; }
}

public sealed record SettledPlayerPose(Guid LocationId, Point Position, double Heading)
{
    public PlayerPose ToIdlePose() =>
        new(
            LocationId,
            Position,
            DateTimeOffset.UnixEpoch,
            new MovementInput(0, 0, Heading),
            TimeSpan.Zero
        );
}

internal class GetSettledPlayerPoseQueryHandler(ICreaturesDbContext context)
    : IQueryHandler<GetSettledPlayerPoseQuery, SettledPlayerPose?>
{
    public Task<SettledPlayerPose?> Handle(
        GetSettledPlayerPoseQuery query,
        CancellationToken cancellationToken = default
    ) =>
        context
            .Creatures.AsNoTracking()
            .Where(creature => creature.Id == query.PlayerId)
            .Select(creature => new SettledPlayerPose(
                creature.LocationId,
                new Point(creature.X, creature.Y),
                creature.Angle
            ))
            .FirstOrDefaultAsync(cancellationToken);
}
