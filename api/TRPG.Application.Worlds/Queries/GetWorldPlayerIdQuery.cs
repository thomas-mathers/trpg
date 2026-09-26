using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data.ModuleContexts;

namespace TRPG.Application.Worlds.Queries;

public class GetWorldPlayerIdQuery
{
    public required Guid WorldId { get; init; }
}

internal class GetWorldPlayerIdQueryHandler(IWorldsDbContext context)
    : IQueryHandler<GetWorldPlayerIdQuery, Guid?>
{
    public async Task<Guid?> Handle(
        GetWorldPlayerIdQuery query,
        CancellationToken cancellationToken = default
    ) =>
        await context
            .Worlds.AsNoTracking()
            .Where(world => world.Id == query.WorldId)
            .Select(world => world.PlayerId)
            .FirstOrDefaultAsync(cancellationToken);
}
