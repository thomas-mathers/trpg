using Microsoft.EntityFrameworkCore;
using TRPG.Data;
using TRPG.Data.Models;

namespace TRPG.Application.Creatures.Queries;

internal class GetCreatureByNameAtLocationQuery
{
    public required Guid WorldId { get; init; }
    public required Guid LocationId { get; init; }
    public required string Name { get; init; }
    public Guid? ExcludingCreatureId { get; init; }
}

internal class GetCreatureByNameAtLocationQueryHandler(TrpgDbContext context)
{
    public async Task<Creature?> Handle(
        GetCreatureByNameAtLocationQuery query,
        CancellationToken cancellationToken = default
    )
    {
        return await context
            .Creatures.AsNoTracking()
            .FirstOrDefaultAsync(
                p =>
                    p.WorldId == query.WorldId
                    && p.LocationId == query.LocationId
                    && p.Name == query.Name
                    && p.Id != query.ExcludingCreatureId,
                cancellationToken
            );
    }
}
