using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data;
using TRPG.Domain.Models;

namespace TRPG.Application.Worlds.Queries;

public class GetWorldQuery
{
    public required Guid WorldId { get; init; }
}

internal class GetWorldQueryHandler(TrpgDbContext context) : IQueryHandler<GetWorldQuery, World?>
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
