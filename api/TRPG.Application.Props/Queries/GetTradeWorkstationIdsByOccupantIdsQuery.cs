using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Props.Queries;

public class GetTradeWorkstationIdsByOccupantIdsQuery
{
    public required IReadOnlyCollection<Guid> OccupantIds { get; init; }
}

internal class GetTradeWorkstationIdsByOccupantIdsQueryHandler(IPropsDbContext context)
    : IQueryHandler<GetTradeWorkstationIdsByOccupantIdsQuery, IReadOnlyDictionary<Guid, Guid?>>
{
    public async Task<IReadOnlyDictionary<Guid, Guid?>> Handle(
        GetTradeWorkstationIdsByOccupantIdsQuery query,
        CancellationToken cancellationToken = default
    ) =>
        await context
            .Props.OfType<Workstation>()
            .AsNoTracking()
            .Where(workstation =>
                query.OccupantIds.AsEnumerable().Contains(workstation.OccupantId ?? Guid.Empty)
                && workstation.WorkstationType == WorkstationType.Trade
            )
            .ToDictionaryAsync(
                workstation => workstation.OccupantId!.Value,
                workstation => (Guid?)workstation.Id,
                cancellationToken
            );
}
