using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Worlds.Queries;

public class GetDistrictsByIdsQuery
{
    public required IReadOnlyCollection<Guid> Ids { get; init; }
}

internal class GetDistrictsByIdsQueryHandler(IWorldsDbContext context)
    : IQueryHandler<GetDistrictsByIdsQuery, IReadOnlyDictionary<Guid, District>>
{
    public async Task<IReadOnlyDictionary<Guid, District>> Handle(
        GetDistrictsByIdsQuery query,
        CancellationToken cancellationToken = default
    ) =>
        await context
            .Districts.AsNoTracking()
            .Where(district => query.Ids.AsEnumerable().Contains(district.Id))
            .ToDictionaryAsync(district => district.Id, cancellationToken);
}
