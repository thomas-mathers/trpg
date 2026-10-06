using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Creatures.Queries;

public class GetPlayerPoseQuery
{
    public required Guid PlayerId { get; init; }
}

internal class GetPlayerPoseQueryHandler(ICreaturesDbContext context, PlayerPoseStore poseStore)
    : IQueryHandler<GetPlayerPoseQuery, Placement?>
{
    public async Task<Placement?> Handle(
        GetPlayerPoseQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var stored = await context
            .Creatures.AsNoTracking()
            .Where(creature => creature.Id == query.PlayerId)
            .Select(creature => new
            {
                creature.LocationId,
                creature.X,
                creature.Y,
                creature.Angle,
            })
            .FirstOrDefaultAsync(cancellationToken);
        if (stored is null)
        {
            return null;
        }

        var live = poseStore.Find(query.PlayerId);
        if (live?.LocationId == stored.LocationId)
        {
            return new Placement(live.X, live.Y, live.Angle);
        }

        return new Placement(stored.X, stored.Y, stored.Angle);
    }
}
