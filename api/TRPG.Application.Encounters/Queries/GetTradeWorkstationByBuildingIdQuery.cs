using TRPG.Application.Common.Queries;
using TRPG.Application.Props.Queries;
using TRPG.Application.Worlds.Queries;
using TRPG.Domain.Models;

namespace TRPG.Application.Encounters.Queries;

public class GetTradeWorkstationByBuildingIdQuery
{
    public required Guid BuildingId { get; init; }
}

internal class GetTradeWorkstationByBuildingIdQueryHandler(
    IQueryHandler<GetRoomsByBuildingIdQuery, IReadOnlyCollection<Room>> getRoomsByBuildingId,
    IQueryHandler<
        GetTradeWorkstationByLocationIdsQuery,
        Workstation?
    > getTradeWorkstationByLocationIds
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

        return await getTradeWorkstationByLocationIds.Handle(
            new GetTradeWorkstationByLocationIdsQuery
            {
                LocationIds = rooms.Select(room => room.LocationId).ToArray(),
            },
            cancellationToken
        );
    }
}
