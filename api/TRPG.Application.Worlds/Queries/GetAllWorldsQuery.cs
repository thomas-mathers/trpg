using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Handling;
using TRPG.Data;
using TRPG.Data.Models;

namespace TRPG.Application.Worlds.Queries;

public class GetAllWorldsQuery;

internal class GetAllWorldsQueryHandler(TrpgDbContext context)
    : IQueryHandler<GetAllWorldsQuery, IReadOnlyList<World>>
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
