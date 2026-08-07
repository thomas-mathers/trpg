using Microsoft.EntityFrameworkCore;
using TRPG.Data;
using TRPG.Data.Models;

namespace TRPG.Application.Worlds.Queries;

internal class GetLocationByIdQuery
{
    public required Guid Id { get; init; }
}

internal class GetLocationByIdQueryHandler(TrpgDbContext context)
{
    public async Task<Location?> Handle(
        GetLocationByIdQuery query,
        CancellationToken cancellationToken = default
    )
    {
        return await context
            .Locations.AsNoTracking()
            .FirstOrDefaultAsync(l => l.Id == query.Id, cancellationToken);
    }
}
