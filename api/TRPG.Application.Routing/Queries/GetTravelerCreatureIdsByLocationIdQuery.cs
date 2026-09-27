using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data.ModuleContexts;

namespace TRPG.Application.Routing.Queries;

public class GetTravelerCreatureIdsByLocationIdQuery
{
    public required Guid WorldId { get; init; }
    public required Guid LocationId { get; init; }
}

internal class GetTravelerCreatureIdsByLocationIdQueryHandler(IRoutingDbContext context)
    : IQueryHandler<GetTravelerCreatureIdsByLocationIdQuery, IReadOnlyList<Guid>>
{
    public async Task<IReadOnlyList<Guid>> Handle(
        GetTravelerCreatureIdsByLocationIdQuery query,
        CancellationToken cancellationToken = default
    ) =>
        await (
            from member in context.RouteTravelerMembers.AsNoTracking()
            join traveler in context.RouteTravelers.AsNoTracking()
                on member.RouteTravelerId equals traveler.Id
            where
                traveler.WorldId == query.WorldId
                && context.RouteSteps.Any(step =>
                    step.RouteId == traveler.RouteId && step.LocationId == query.LocationId
                )
            select member.CreatureId
        )
            .Distinct()
            .ToArrayAsync(cancellationToken);
}
