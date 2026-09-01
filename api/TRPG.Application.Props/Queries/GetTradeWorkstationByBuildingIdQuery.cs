using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Application.Worlds.Queries;
using TRPG.Data;
using TRPG.Domain.Models;

namespace TRPG.Application.Props.Queries;

public class GetTradeWorkstationByBuildingIdQuery
{
    public required Guid BuildingId { get; init; }
}

internal class GetTradeWorkstationByBuildingIdQueryHandler(
    TrpgDbContext context,
    IQueryHandler<GetRoomsByBuildingIdQuery, IReadOnlyCollection<Room>> getRoomsByBuildingId
) : IQueryHandler<GetTradeWorkstationByBuildingIdQuery, Workstation?>
{
    public async Task<Workstation?> Handle(
        GetTradeWorkstationByBuildingIdQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var rooms = await getRoomsByBuildingId.Handle(
            new GetRoomsByBuildingIdQuery { BuildingId = query.BuildingId },
            cancellationToken
        );
        var locationIds = rooms.Select(room => room.LocationId).ToArray();

        return await context
            .Props.OfType<Workstation>()
            .AsNoTracking()
            .Where(workstation =>
                workstation.WorkstationType == WorkstationType.Trade
                && locationIds.AsEnumerable().Contains(workstation.LocationId)
            )
            .FirstOrDefaultAsync(cancellationToken);
    }
}
