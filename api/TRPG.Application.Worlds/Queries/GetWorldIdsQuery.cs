using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data.ModuleContexts;

namespace TRPG.Application.Worlds.Queries;

public class GetWorldIdsQuery;

internal class GetWorldIdsQueryHandler(IWorldsDbContext context)
    : IQueryHandler<GetWorldIdsQuery, IReadOnlyCollection<Guid>>
{
    public async Task<IReadOnlyCollection<Guid>> Handle(
        GetWorldIdsQuery query,
        CancellationToken cancellationToken = default
    ) =>
        await context
            .Worlds.AsNoTracking()
            .Select(world => world.Id)
            .ToArrayAsync(cancellationToken);
}
