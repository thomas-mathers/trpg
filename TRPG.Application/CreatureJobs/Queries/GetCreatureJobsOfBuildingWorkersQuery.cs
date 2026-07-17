using Microsoft.EntityFrameworkCore;
using TRPG.Data;
using TRPG.Data.Models;

namespace TRPG.Application.CreatureJobs.Queries;

internal class GetCreatureJobsOfBuildingWorkersQuery
{
    public required Guid BuildingId { get; init; }
}

internal class GetCreatureJobsOfBuildingWorkersQueryHandler(TrpgDbContext context)
{
    public async Task<IReadOnlyList<CreatureJob>> Handle(
        GetCreatureJobsOfBuildingWorkersQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var workerIds =
            from job in context.CreatureJobs
            join room in context.Rooms on job.RoomId equals room.Id
            where room.BuildingId == query.BuildingId && job.Action == CreatureJobAction.Work
            select job.CreatureId;

        return await context
            .CreatureJobs.AsNoTracking()
            .Where(j => workerIds.Contains(j.CreatureId))
            .ToArrayAsync(cancellationToken);
    }
}
