using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Worlds.Queries;

public class GetDistrictsByCityIdQuery
{
    public required Guid CityId { get; init; }
}

internal class GetDistrictsByCityIdQueryHandler(IWorldsDbContext context)
    : IQueryHandler<GetDistrictsByCityIdQuery, IReadOnlyCollection<District>>
{
    public async Task<IReadOnlyCollection<District>> Handle(
        GetDistrictsByCityIdQuery query,
        CancellationToken cancellationToken = default
    ) =>
        await context
            .Districts.AsNoTracking()
            .Where(d => d.CityId == query.CityId)
            .ToArrayAsync(cancellationToken);
}
