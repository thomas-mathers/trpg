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
        return await context
            .Props.AsNoTracking()
            .OfType<Workstation>()
            .Where(workstation => query.LocationIds.AsEnumerable().Contains(workstation.LocationId))
            .Select(workstation => workstation.Id)
            .ToArrayAsync(cancellationToken);
    }
}
