using Microsoft.EntityFrameworkCore;
using TRPG.Data;
using TRPG.Data.Models;

namespace TRPG.Application.Creatures.Queries;

internal class GetCreatureByNameOutdoorsInDistrictQuery
{
    public required Guid WorldId { get; init; }
    public required Guid DistrictId { get; init; }
    public required string Name { get; init; }
}

internal class GetCreatureByNameOutdoorsInDistrictQueryHandler(TrpgDbContext context)
{
    public async Task<Creature?> Handle(
        GetCreatureByNameOutdoorsInDistrictQuery query,
        CancellationToken cancellationToken = default
    )
    {
        return await context
            .Creatures.AsNoTracking()
            .FirstOrDefaultAsync(
                p =>
                    p.WorldId == query.WorldId
                    && p.DistrictId == query.DistrictId
                    && p.RoomId == null
                    && p.Name == query.Name,
                cancellationToken
            );
    }
}
