using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data.ModuleContexts;

namespace TRPG.Application.GuardPatrols.Queries;

public class GetGuardPatrolMembersByRouteTravelerIdsQuery
{
    public required IReadOnlyCollection<Guid> RouteTravelerIds { get; init; }
}

internal class GetGuardPatrolMembersByRouteTravelerIdsQueryHandler(IGuardPatrolsDbContext context)
    : IQueryHandler<
        GetGuardPatrolMembersByRouteTravelerIdsQuery,
        IReadOnlyDictionary<Guid, IReadOnlyList<Guid>>
    >
{
    public async Task<IReadOnlyDictionary<Guid, IReadOnlyList<Guid>>> Handle(
        GetGuardPatrolMembersByRouteTravelerIdsQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var members = await context
            .GuardPatrolMembers.AsNoTracking()
            .Where(m => query.RouteTravelerIds.AsEnumerable().Contains(m.RouteTravelerId))
            .ToArrayAsync(cancellationToken);

        return members
            .GroupBy(m => m.RouteTravelerId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<Guid>)group.Select(m => m.CreatureId).ToArray()
            );
    }
}
