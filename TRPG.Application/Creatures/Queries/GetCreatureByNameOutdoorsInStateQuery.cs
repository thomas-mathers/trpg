using Microsoft.EntityFrameworkCore;
using TRPG.Data;
using TRPG.Data.Models;

namespace TRPG.Application.Creatures.Queries;

internal class GetCreatureByNameOutdoorsInStateQuery
{
    public required Guid WorldId { get; init; }
    public required Guid StateId { get; init; }
    public required string Name { get; init; }
}

internal class GetCreatureByNameOutdoorsInStateQueryHandler(TrpgDbContext context)
{
    public async Task<Creature?> Handle(
        GetCreatureByNameOutdoorsInStateQuery query,
        CancellationToken cancellationToken = default
    )
    {
        return await context
            .Creatures.AsNoTracking()
            .FirstOrDefaultAsync(
                p =>
                    p.WorldId == query.WorldId
                    && p.StateId == query.StateId
                    && p.RoomId == null
                    && p.Name == query.Name,
                cancellationToken
            );
    }
}
