using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data;
using TRPG.Domain.Models;

namespace TRPG.Application.Locations.Queries;

public class GetLocationsByIdsQuery
{
    public required IReadOnlyCollection<Guid> Ids { get; init; }
}

internal class GetLocationsByIdsQueryHandler(TrpgDbContext context)
    : IQueryHandler<GetLocationsByIdsQuery, IReadOnlyDictionary<Guid, Location>>
{
    public async Task<IReadOnlyDictionary<Guid, Location>> Handle(
        GetLocationsByIdsQuery query,
        CancellationToken cancellationToken = default
    ) =>
        await context
            .Locations.AsNoTracking()
            .Where(location => query.Ids.AsEnumerable().Contains(location.Id))
            .ToDictionaryAsync(location => location.Id, cancellationToken);
}
