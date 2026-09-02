using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Props.Queries;

public class GetTradeWorkstationByLocationIdsQuery
{
    public required IReadOnlyCollection<Guid> LocationIds { get; init; }
}

internal class GetTradeWorkstationByLocationIdsQueryHandler(IPropsDbContext context)
    : IQueryHandler<GetTradeWorkstationByLocationIdsQuery, Workstation?>
{
    public async Task<Workstation?> Handle(
        GetTradeWorkstationByLocationIdsQuery query,
        CancellationToken cancellationToken = default
    ) =>
        await context
            .Props.OfType<Workstation>()
            .AsNoTracking()
            .Where(workstation =>
                workstation.WorkstationType == WorkstationType.Trade
                && query.LocationIds.AsEnumerable().Contains(workstation.LocationId)
            )
            .FirstOrDefaultAsync(cancellationToken);
}
