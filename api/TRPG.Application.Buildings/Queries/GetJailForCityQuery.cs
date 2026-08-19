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
        var jail = await context
            .Buildings.AsNoTracking()
            .Join(
                context.Locations.AsNoTracking(),
                building => building.ExteriorLocationId,
                location => location.Id,
                (building, location) => new { building, location }
            )
            .Where(x =>
                x.building.BuildingType == BuildingType.Jail && x.location.CityId == query.CityId
            )
            .Select(x => x.building)
            .FirstOrDefaultAsync(cancellationToken);

        if (jail == null)
        {
            return null;
        }

        var cellsRoom = await context
            .Rooms.AsNoTracking()
            .FirstOrDefaultAsync(
                r => r.BuildingId == jail.Id && r.Name == "Cells",
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
