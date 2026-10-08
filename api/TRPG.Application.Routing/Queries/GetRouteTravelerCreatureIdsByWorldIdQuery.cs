using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data.ModuleContexts;

namespace TRPG.Application.Routing.Queries;

public class GetRouteTravelerCreatureIdsByWorldIdQuery
{
    public required Guid WorldId { get; init; }
}

internal class GetRouteTravelerCreatureIdsByWorldIdQueryHandler(IRoutingDbContext context)
    : IQueryHandler<GetRouteTravelerCreatureIdsByWorldIdQuery, IReadOnlyCollection<Guid>>
{
    public async Task<IReadOnlyCollection<Guid>> Handle(
        GetRouteTravelerCreatureIdsByWorldIdQuery query,
        CancellationToken cancellationToken = default
    ) =>
        await context
            .RouteTravelerMembers.AsNoTracking()
            .Where(member => member.WorldId == query.WorldId)
            .Select(member => member.CreatureId)
            .Distinct()
            .ToArrayAsync(cancellationToken);
}
