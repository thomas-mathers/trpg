using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Props.Queries;

public class GetBedsByLocationIdsQuery
{
    public required IReadOnlyCollection<Guid> LocationIds { get; init; }
}

internal class GetBedsByLocationIdsQueryHandler(IPropsDbContext context)
    : IQueryHandler<GetBedsByLocationIdsQuery, IReadOnlyDictionary<Guid, Bed>>
{
    public async Task<IReadOnlyDictionary<Guid, Bed>> Handle(
        GetBedsByLocationIdsQuery query,
        CancellationToken cancellationToken = default
    )
    {
        if (query.LocationIds.Count == 0)
        {
            return new Dictionary<Guid, Bed>();
        }

        var beds = await context
            .Props.AsNoTracking()
            .OfType<Bed>()
            .Where(bed => query.LocationIds.AsEnumerable().Contains(bed.LocationId))
            .ToArrayAsync(cancellationToken);

        return beds.GroupBy(bed => bed.LocationId)
            .ToDictionary(group => group.Key, group => group.First());
    }
}
