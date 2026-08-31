using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Application.Worlds.Queries;
using TRPG.Data;
using TRPG.Domain.Models;

namespace TRPG.Application.CreatureJobs.Queries;

public class GetCreatureJobsOfBuildingWorkersQuery
{
    public required Guid BuildingId { get; init; }
}

internal class GetCreatureJobsOfBuildingWorkersQueryHandler(
    TrpgDbContext context,
    IQueryHandler<GetRoomsByBuildingIdQuery, IReadOnlyCollection<Room>> getRoomsByBuildingId
) : IQueryHandler<GetCreatureJobsOfBuildingWorkersQuery, IReadOnlyList<CreatureJob>>
{
    public async Task<IReadOnlyList<CreatureJob>> Handle(
        GetCreatureJobsOfBuildingWorkersQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var rooms = await getRoomsByBuildingId.Handle(
            new GetRoomsByBuildingIdQuery { BuildingId = query.BuildingId },
            cancellationToken
        );
        var roomLocationIds = rooms.Select(room => room.LocationId).ToArray();

        var workerIds = await context
            .CreatureJobs.Where(job =>
                job.Action == CreatureJobAction.Work
                && roomLocationIds.AsEnumerable().Contains(job.LocationId)
            )
            .Select(job => job.CreatureId)
            .ToArrayAsync(cancellationToken);

        return await context
            .CreatureJobs.AsNoTracking()
            .Where(j => workerIds.AsEnumerable().Contains(j.CreatureId))
            .ToArrayAsync(cancellationToken);
    }
}
