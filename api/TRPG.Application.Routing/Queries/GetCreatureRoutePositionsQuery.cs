using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data.ModuleContexts;
using TRPG.Domain;
using TRPG.Domain.Models;

namespace TRPG.Application.Routing.Queries;

public record CreatureRoutePosition(
    Guid RouteTravelerId,
    Guid RouteId,
    RouteTraversal Traversal,
    string? Purpose,
    RouteTimelinePosition Position
);

public class GetCreatureRoutePositionsQuery
{
    public required IReadOnlyCollection<Guid> CreatureIds { get; init; }
    public required TimeSpan Playtime { get; init; }
}

internal class GetCreatureRoutePositionsQueryHandler(
    IRoutingDbContext context,
    IQueryHandler<
        ResolveRouteTravelerPositionsQuery,
        IReadOnlyDictionary<Guid, ResolvedRouteTravelerPosition>
    > resolvePositions
) : IQueryHandler<GetCreatureRoutePositionsQuery, IReadOnlyDictionary<Guid, CreatureRoutePosition>>
{
    public async Task<IReadOnlyDictionary<Guid, CreatureRoutePosition>> Handle(
        GetCreatureRoutePositionsQuery query,
        CancellationToken cancellationToken = default
    )
    {
        if (query.CreatureIds.Count == 0)
        {
            return new Dictionary<Guid, CreatureRoutePosition>();
        }

        var memberships = await context
            .RouteTravelerMembers.AsNoTracking()
            .Where(member => query.CreatureIds.AsEnumerable().Contains(member.CreatureId))
            .ToArrayAsync(cancellationToken);
        var travelerIds = memberships.Select(member => member.RouteTravelerId).ToArray();
        if (travelerIds.Length == 0)
        {
            return new Dictionary<Guid, CreatureRoutePosition>();
        }

        var travelers = await context
            .RouteTravelers.AsNoTracking()
            .Where(traveler => travelerIds.AsEnumerable().Contains(traveler.Id))
            .ToDictionaryAsync(traveler => traveler.Id, cancellationToken);
        var routeIds = travelers.Values.Select(traveler => traveler.RouteId).Distinct().ToArray();
        var routes = await context
            .Routes.AsNoTracking()
            .Where(route => routeIds.AsEnumerable().Contains(route.Id))
            .ToDictionaryAsync(route => route.Id, cancellationToken);
        var positions = await resolvePositions.Handle(
            new ResolveRouteTravelerPositionsQuery
            {
                RouteTravelerIds = travelerIds,
                Playtime = query.Playtime,
            },
            cancellationToken
        );

        return memberships.ToDictionary(
            membership => membership.CreatureId,
            membership =>
            {
                var traveler = travelers[membership.RouteTravelerId];
                return new CreatureRoutePosition(
                    traveler.Id,
                    traveler.RouteId,
                    routes[traveler.RouteId].Traversal,
                    traveler.Purpose,
                    positions[traveler.Id].Position
                );
            }
        );
    }
}
