using Microsoft.EntityFrameworkCore;
using TRPG.Data;
using TRPG.Data.Models;

namespace TRPG.Application.Worlds.Queries;

internal class GetAllCountriesByWorldIdQuery
{
    public required Guid WorldId { get; init; }
}

internal class GetAllCountriesByWorldIdQueryHandler(TrpgDbContext context)
{
    public async Task<IReadOnlyCollection<Country>> Handle(
        GetAllCountriesByWorldIdQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var list = await context
            .Countries.AsNoTracking()
            .Where(c => c.WorldId == query.WorldId)
            .ToArrayAsync(cancellationToken);
        return list;
    }
}
