using Microsoft.EntityFrameworkCore;
using TRPG.Data;
using TRPG.Data.Models;

namespace TRPG.Application.Worlds.Queries;

public class ListWorldsQuery;

public class ListWorldsQueryHandler(TrpgDbContext context)
{
    public async Task<IReadOnlyList<World>> Handle(
        ListWorldsQuery query,
        CancellationToken cancellationToken = default
    )
    {
        return await context
            .Worlds.AsNoTracking()
            .OrderBy(w => w.Name)
            .ToArrayAsync(cancellationToken);
    }
}
