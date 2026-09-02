using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Creatures.Queries;

public class GetCreatureByIdQuery
{
    public required Guid Id { get; init; }
}

internal class GetCreatureByIdQueryHandler(ICreaturesDbContext context)
    : IQueryHandler<GetCreatureByIdQuery, Creature?>
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
