using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Props.Queries;

public class GetWorkstationsByLocationIdsQuery
{
    public required IReadOnlyCollection<Guid> LocationIds { get; init; }
}

internal class GetWorkstationsByLocationIdsQueryHandler(IPropsDbContext context)
    : IQueryHandler<
        GetWorkstationsByLocationIdsQuery,
        IReadOnlyDictionary<Guid, IReadOnlyList<Workstation>>
    >
{
    public async Task<IReadOnlyDictionary<Guid, IReadOnlyList<Workstation>>> Handle(
        GetWorkstationsByLocationIdsQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var workstations = await context
            .Props.AsNoTracking()
            .OfType<Workstation>()
            .Where(workstation => query.LocationIds.AsEnumerable().Contains(workstation.LocationId))
            .ToArrayAsync(cancellationToken);

        return workstations
            .GroupBy(workstation => workstation.LocationId)
            .ToDictionary(
                group => group.Key,
                IReadOnlyList<Workstation> (group) => group.ToArray()
            );
    }
}
