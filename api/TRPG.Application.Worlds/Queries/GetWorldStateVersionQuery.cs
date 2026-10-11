using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Exceptions;
using TRPG.Application.Common.Queries;
using TRPG.Data.ModuleContexts;

namespace TRPG.Application.Worlds.Queries;

public class GetWorldStateVersionQuery
{
    public required Guid WorldId { get; init; }
}

internal class GetWorldStateVersionQueryHandler(IWorldsDbContext context)
    : IQueryHandler<GetWorldStateVersionQuery, long>
{
    public async Task<long> Handle(
        GetWorldStateVersionQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var version = await context
            .Worlds.AsNoTracking()
            .Where(world => world.Id == query.WorldId)
            .Select(world => (long?)world.StateVersion)
            .SingleOrDefaultAsync(cancellationToken);

        return version ?? throw new EntityNotFoundException("World", query.WorldId);
    }
}
