using Microsoft.EntityFrameworkCore;
using TRPG.Data;

namespace TRPG.Application.Jobs.Queries;

internal class GetCreatureIdsWithJobInRoomQuery
{
    public required Guid RoomId { get; init; }
}

internal class GetCreatureIdsWithJobInRoomQueryHandler(TrpgDbContext context)
{
    public async Task<IReadOnlyList<Guid>> Handle(
        GetCreatureIdsWithJobInRoomQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var ids = await context
            .Jobs.Where(j => j.RoomId == query.RoomId)
            .Select(j => j.CreatureId)
            .Distinct()
            .ToArrayAsync(cancellationToken);
        return ids;
    }
}
