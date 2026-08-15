using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using TRPG.Application.Common.Handling;
using TRPG.Data;
using TRPG.Data.Models;

namespace TRPG.Application.Worlds.Queries;

public class GetStateByIdQuery
{
    public required Guid Id { get; init; }
}

public class GetStateByIdQueryHandler(TrpgDbContext context, IMemoryCache cache)
    : IQueryHandler<GetStateByIdQuery, State?>
{
    public async Task<State?> Handle(
        GetStateByIdQuery query,
        CancellationToken cancellationToken = default
    )
    {
        return await cache.GetOrCreateAsync(
            $"state:{query.Id}",
            _ =>
                context
                    .States.AsNoTracking()
                    .FirstOrDefaultAsync(s => s.Id == query.Id, cancellationToken)
        );
    }
}
