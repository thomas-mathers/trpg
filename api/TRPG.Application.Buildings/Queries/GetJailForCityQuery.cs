using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data;
using TRPG.Domain.Models;

namespace TRPG.Application.Buildings.Queries;

public class GetJailForCityQuery
{
    public required Guid CityId { get; init; }
}

public record JailInfo(Guid CellsLocationId, IReadOnlyList<Guid> CellDoorConnectorIds);

internal class GetJailForCityQueryHandler(TrpgDbContext context)
    : IQueryHandler<GetJailForCityQuery, JailInfo?>
{
    public async Task<JailInfo?> Handle(
        GetJailForCityQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var jailId = await (
            from building in context.Buildings.AsNoTracking()
            join location in context.Locations.AsNoTracking()
                on building.ExteriorLocationId equals location.Id
            where building.BuildingType == BuildingType.Jail && location.CityId == query.CityId
            select (Guid?)building.Id
        ).FirstOrDefaultAsync(cancellationToken);

        if (jailId == null)
        {
            return null;
        }

        var cellsRoom = await context
            .Rooms.AsNoTracking()
            .FirstOrDefaultAsync(
                r => r.BuildingId == jailId.Value && r.Name == JailRoomNames.Cells,
                cancellationToken
            );

        if (cellsRoom == null)
        {
            return null;
        }

        var cellDoorConnectorIds = await (
            from door in context.DoorConnectors.AsNoTracking()
            join connector in context.LocationConnectors.AsNoTracking()
                on door.ConnectorId equals connector.Id
            where
                connector.OriginLocationId == cellsRoom.LocationId
                || connector.DestinationLocationId == cellsRoom.LocationId
            select door.Id
        ).ToArrayAsync(cancellationToken);

        return new JailInfo(cellsRoom.LocationId, cellDoorConnectorIds);
    }
}
