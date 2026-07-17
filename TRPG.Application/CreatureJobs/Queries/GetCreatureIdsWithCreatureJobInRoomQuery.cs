using Microsoft.EntityFrameworkCore;
using TRPG.Data;

namespace TRPG.Application.CreatureJobs.Queries;

internal class GetCreatureIdsWithCreatureJobInRoomQuery
{
    public required Guid RoomId { get; init; }
}

internal class GetCreatureIdsWithCreatureJobInRoomQueryHandler(TrpgDbContext context)
{
    public async Task<IReadOnlyList<Guid>> Handle(
        GetCreatureIdsWithCreatureJobInRoomQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var ids = await context
            .CreatureJobs.Where(j => j.RoomId == query.RoomId)
            .Select(j => j.CreatureId)
            .Distinct()
            .ToArrayAsync(cancellationToken);
        return ids;
    }
}
