using Microsoft.EntityFrameworkCore;
using TRPG.Data;
using TRPG.Data.Models;

namespace TRPG.Application.Jobs.Queries;

internal class GetJobsOfBuildingWorkersQuery
{
    public required Guid BuildingId { get; init; }
}

internal class GetJobsOfBuildingWorkersQueryHandler(TrpgDbContext context)
{
    public async Task<IReadOnlyList<Job>> Handle(
        GetJobsOfBuildingWorkersQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var workerIds =
            from job in context.Jobs
            join room in context.Rooms on job.RoomId equals room.Id
            where room.BuildingId == query.BuildingId && job.Action == JobAction.Work
            select job.CreatureId;

        return await context
            .Jobs.AsNoTracking()
            .Where(j => workerIds.Contains(j.CreatureId))
            .ToArrayAsync(cancellationToken);
    }
}
