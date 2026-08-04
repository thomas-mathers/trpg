using TRPG.Data;
using TRPG.Data.Models;

namespace TRPG.Application.Buildings.Queries;

internal static class LocationConnectorQueryExtensions
{
    public static IQueryable<LocationConnector> WhereLeadsOutside(
        this IQueryable<LocationConnector> connectors,
        TrpgDbContext context
    ) =>
        from c in connectors
        join loc in context.Locations on c.DestinationLocationId equals loc.Id
        where loc.RoomId == null
        select c;
}
