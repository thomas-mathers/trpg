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
        var cellsLocationId = await (
            from building in context.Buildings.AsNoTracking()
            join location in context.Locations.AsNoTracking()
                on building.ExteriorLocationId equals location.Id
            join room in context.Rooms.AsNoTracking() on building.Id equals room.BuildingId
            where
                building.BuildingType == BuildingType.Jail
                && location.CityId == query.CityId
                && room.Name == JailRoomNames.Cells
            select (Guid?)room.LocationId
        ).FirstOrDefaultAsync(cancellationToken);

        if (cellsLocationId == null)
        {
            return null;
        }

        var cellDoorConnectorIds = await (
            from door in context.DoorConnectors.AsNoTracking()
            join connector in context.LocationConnectors.AsNoTracking()
                on door.ConnectorId equals connector.Id
            where
                connector.OriginLocationId == cellsLocationId.Value
                || connector.DestinationLocationId == cellsLocationId.Value
            select door.Id
        ).ToArrayAsync(cancellationToken);

        return new JailInfo(cellsLocationId.Value, cellDoorConnectorIds);
    }
}
