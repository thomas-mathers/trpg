using Microsoft.EntityFrameworkCore;
using TRPG.Data;
using TRPG.Data.Models;

namespace TRPG.Application.Worlds.Queries;

public class GetAllWorldsQuery;

public class GetAllWorldsQueryHandler(TrpgDbContext context)
{
    public async Task<IReadOnlyList<World>> Handle(
        GetAllWorldsQuery query,
        CancellationToken cancellationToken = default
    )
    {
        return await context
            .Worlds.AsNoTracking()
            .OrderBy(w => w.Name)
            .ToArrayAsync(cancellationToken);
    }
}
