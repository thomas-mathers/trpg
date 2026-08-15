using Microsoft.EntityFrameworkCore;
using TRPG.Data;
using TRPG.Data.Models;

namespace TRPG.Application.Creatures.Queries;

public class GetCreatureByIdQuery
{
    public required Guid Id { get; init; }
}

public class GetCreatureByIdQueryHandler(TrpgDbContext context)
{
    public async Task<Creature?> Handle(
        GetCreatureByIdQuery query,
        CancellationToken cancellationToken = default
    )
    {
        return await context
            .Creatures.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == query.Id, cancellationToken);
    }
}
