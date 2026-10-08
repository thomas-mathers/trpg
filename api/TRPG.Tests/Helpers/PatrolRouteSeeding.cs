using TRPG.Data;
using TRPG.Domain.Models;

namespace TRPG.Tests.Helpers;

public static class PatrolRouteSeeding
{
    public static async Task<Route> AddPatrolRoute(
        this TrpgDbContext context,
        Guid worldId,
        LocationConnector outboundDoor,
        CancellationToken cancellationToken
    )
    {
        var returnDoor = Builders.MakeLocationConnector(
            outboundDoor.DestinationLocationId,
            outboundDoor.OriginLocationId,
            worldId: worldId
        );
        var route = Builders.MakeCaravanRoute(worldId);
        context.LocationConnectors.Add(returnDoor);
        context.TravelNodes.AddRange(
            Builders.MakeExitNode(returnDoor, 8, 9),
            Builders.MakeArrivalNode(returnDoor, 1, 2)
        );
        context.PointConnectors.AddRange(
            Builders.MakePointConnector(
                outboundDoor.DestinationLocationId,
                outboundDoor.DestinationNodeId,
                returnDoor.OriginNodeId,
                100,
                worldId
            ),
            Builders.MakePointConnector(
                outboundDoor.OriginLocationId,
                returnDoor.DestinationNodeId,
                outboundDoor.OriginNodeId,
                100,
                worldId
            )
        );
        context.Routes.Add(route);
        var workplaceStop = Builders.MakeCaravanRouteStop(
            route.Id,
            0,
            outboundDoor.DestinationLocationId,
            returnDoor.Id
        );
        var homeStop = Builders.MakeCaravanRouteStop(
            route.Id,
            1,
            outboundDoor.OriginLocationId,
            outboundDoor.Id
        );
        workplaceStop.TravelNodeId = outboundDoor.DestinationNodeId;
        homeStop.TravelNodeId = outboundDoor.OriginNodeId;
        context.RouteSteps.AddRange(workplaceStop, homeStop);
        await context.SaveChangesAsync(cancellationToken);

        return route;
    }
}
