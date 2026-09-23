using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data.ModuleContexts;

namespace TRPG.Application.Routing.Queries;

public class GetRouteTravelerMembersByRouteTravelerIdsQuery
{
    public required IReadOnlyCollection<Guid> RouteTravelerIds { get; init; }
}

internal class GetRouteTravelerMembersByRouteTravelerIdsQueryHandler(IRoutingDbContext context)
    : IQueryHandler<
        GetRouteTravelerMembersByRouteTravelerIdsQuery,
        IReadOnlyDictionary<Guid, IReadOnlyList<Guid>>
    >
{
    public async Task<IReadOnlyDictionary<Guid, IReadOnlyList<Guid>>> Handle(
        GetRouteTravelerMembersByRouteTravelerIdsQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var members = await context
            .RouteTravelerMembers.AsNoTracking()
            .Where(member => query.RouteTravelerIds.AsEnumerable().Contains(member.RouteTravelerId))
            .ToArrayAsync(cancellationToken);

        return members
            .GroupBy(member => member.RouteTravelerId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<Guid>)group.Select(member => member.CreatureId).ToArray()
            );
    }
}
