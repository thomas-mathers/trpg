using Microsoft.EntityFrameworkCore;
using TRPG.Data;
using TRPG.Data.Models;

namespace TRPG.Application.Worlds.Queries;

public class GetWorldQuery
{
    public required Guid WorldId { get; init; }
}

public class GetWorldQueryHandler(TrpgDbContext context)
{
    public async Task<World?> Handle(
        GetWorldQuery query,
        CancellationToken cancellationToken = default
    )
    {
        return await context
            .Worlds.AsNoTracking()
            .FirstOrDefaultAsync(w => w.Id == query.WorldId, cancellationToken);
    }
}
