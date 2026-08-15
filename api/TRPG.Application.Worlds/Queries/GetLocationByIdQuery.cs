using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Handling;
using TRPG.Data;
using TRPG.Data.Models;

namespace TRPG.Application.Worlds.Queries;

public class GetLocationByIdQuery
{
    public required Guid Id { get; init; }
}

internal class GetLocationByIdQueryHandler(TrpgDbContext context)
    : IQueryHandler<GetLocationByIdQuery, Location?>
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
