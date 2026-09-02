using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Worlds.Queries;

public class GetBuildingsByWorldIdQuery
{
    public required Guid WorldId { get; init; }
}

internal class GetBuildingsByWorldIdQueryHandler(IWorldsDbContext context)
    : IQueryHandler<GetBuildingsByWorldIdQuery, IReadOnlyCollection<Building>>
{
    public async Task<IReadOnlyCollection<Building>> Handle(
        GetBuildingsByWorldIdQuery query,
        CancellationToken cancellationToken = default
    ) =>
        await context
            .Buildings.AsNoTracking()
            .Where(building => building.WorldId == query.WorldId)
            .ToArrayAsync(cancellationToken);
}
