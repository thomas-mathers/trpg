using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Creatures.Queries;

public class GetCreatureByNameAtLocationQuery
{
    public required Guid WorldId { get; init; }
    public required Guid LocationId { get; init; }
    public required string Name { get; init; }
    public Guid? ExcludingCreatureId { get; init; }
}

internal class GetCreatureByNameAtLocationQueryHandler(ICreaturesDbContext context)
    : IQueryHandler<GetCreatureByNameAtLocationQuery, Creature?>
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
