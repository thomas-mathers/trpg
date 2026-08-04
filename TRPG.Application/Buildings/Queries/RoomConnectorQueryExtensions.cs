using TRPG.Data;
using TRPG.Data.Models;

namespace TRPG.Application.Buildings.Queries;

internal static class RoomConnectorQueryExtensions
{
    public static IQueryable<RoomConnector> WhereLeadsOutside(
        this IQueryable<RoomConnector> connectors,
        TrpgDbContext context
    ) =>
        from c in connectors
        join loc in context.Locations on c.DestinationLocationId equals loc.Id
        where loc.RoomId == null
        select c;
}
