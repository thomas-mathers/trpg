using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data;
using TRPG.Domain.Models;

namespace TRPG.Application.Locations.Queries;

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
