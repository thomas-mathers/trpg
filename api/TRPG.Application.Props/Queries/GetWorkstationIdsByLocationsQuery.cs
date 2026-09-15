using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Props.Queries;

public class GetWorkstationIdsByLocationsQuery
{
    public required IReadOnlyCollection<Guid> LocationIds { get; init; }
}

internal class GetWorkstationIdsByLocationsQueryHandler(IPropsDbContext context)
    : IQueryHandler<GetWorkstationIdsByLocationsQuery, IReadOnlyList<Guid>>
{
    public async Task<IReadOnlyList<Guid>> Handle(
        GetWorkstationIdsByLocationsQuery query,
        CancellationToken cancellationToken = default
    )
    {
        // Trade only: the client's nearby-workstations panel offers a loot/transfer interaction
        // solely for Trade workstations, so any other type would be an uninteractable steal target.
        return await context
            .Props.AsNoTracking()
            .OfType<Workstation>()
            .Where(workstation => workstation.WorkstationType == WorkstationType.Trade)
            .Where(workstation => query.LocationIds.AsEnumerable().Contains(workstation.LocationId))
            .Select(workstation => workstation.Id)
            .ToArrayAsync(cancellationToken);
    }
}
