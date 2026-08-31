using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data;
using TRPG.Domain.Models;

namespace TRPG.Application.Props.Queries;

public class GetTradeWorkstationByBuildingIdQuery
{
    public required Guid BuildingId { get; init; }
}

internal class GetTradeWorkstationByBuildingIdQueryHandler(TrpgDbContext context)
    : IQueryHandler<GetTradeWorkstationByBuildingIdQuery, Workstation?>
{
    public async Task<Workstation?> Handle(
        GetTradeWorkstationByBuildingIdQuery query,
        CancellationToken cancellationToken = default
    ) =>
        await (
            from workstation in context.Props.OfType<Workstation>().AsNoTracking()
            where workstation.WorkstationType == WorkstationType.Trade
            join room in context.Rooms.AsNoTracking()
                on workstation.LocationId equals room.LocationId
            where room.BuildingId == query.BuildingId
            select workstation
        ).FirstOrDefaultAsync(cancellationToken);
}
