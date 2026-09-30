using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data.ModuleContexts;

namespace TRPG.Application.Routing.Queries;

public record RouteTravelerIdentity(Guid RouteTravelerId, Guid WorldId, Guid RouteId);

public class GetRouteTravelerIdentityQuery
{
    public required Guid RouteTravelerId { get; init; }
}

internal class GetRouteTravelerIdentityQueryHandler(IRoutingDbContext context)
    : IQueryHandler<GetRouteTravelerIdentityQuery, RouteTravelerIdentity?>
{
    public async Task<RouteTravelerIdentity?> Handle(
        GetRouteTravelerIdentityQuery query,
        CancellationToken cancellationToken = default
    ) =>
        await context
            .RouteTravelers.AsNoTracking()
            .Where(traveler => traveler.Id == query.RouteTravelerId)
            .Select(traveler => new RouteTravelerIdentity(
                traveler.Id,
                traveler.WorldId,
                traveler.RouteId
            ))
            .SingleOrDefaultAsync(cancellationToken);
}
