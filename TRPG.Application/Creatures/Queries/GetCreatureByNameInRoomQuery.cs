using Microsoft.EntityFrameworkCore;
using TRPG.Data;
using TRPG.Data.Models;

namespace TRPG.Application.Creatures.Queries;

internal class GetCreatureByNameInRoomQuery
{
    public required Guid WorldId { get; init; }
    public required Guid RoomId { get; init; }
    public required string Name { get; init; }
}

internal class GetCreatureByNameInRoomQueryHandler(TrpgDbContext context)
{
    public async Task<Creature?> Handle(
        GetCreatureByNameInRoomQuery query,
        CancellationToken cancellationToken = default
    )
    {
        return await context
            .Creatures.AsNoTracking()
            .FirstOrDefaultAsync(
                p => p.WorldId == query.WorldId && p.RoomId == query.RoomId && p.Name == query.Name,
                cancellationToken
            );
    }
}
