using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Worlds.Queries;

public class GetDistrictsByWorldIdQuery
{
    public required Guid WorldId { get; init; }
}

internal class GetDistrictsByWorldIdQueryHandler(IWorldsDbContext context)
    : IQueryHandler<GetDistrictsByWorldIdQuery, IReadOnlyCollection<District>>
{
    public async Task<IReadOnlyCollection<District>> Handle(
        GetDistrictsByWorldIdQuery query,
        CancellationToken cancellationToken = default
    ) =>
        await context
            .Districts.AsNoTracking()
            .Where(district => district.WorldId == query.WorldId)
            .ToArrayAsync(cancellationToken);
}
