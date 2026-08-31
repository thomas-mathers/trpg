using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data;

namespace TRPG.Application.Worlds.Queries;

public class GetFactionIdsByCreatureIdsQuery
{
    public required IReadOnlyCollection<Guid> CreatureIds { get; init; }
}

internal class GetFactionIdsByCreatureIdsQueryHandler(TrpgDbContext context)
    : IQueryHandler<GetFactionIdsByCreatureIdsQuery, IReadOnlyDictionary<Guid, IReadOnlyList<Guid>>>
{
    public async Task<IReadOnlyDictionary<Guid, IReadOnlyList<Guid>>> Handle(
        GetFactionIdsByCreatureIdsQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var memberships = await context
            .FactionMembers.AsNoTracking()
            .Where(member => query.CreatureIds.AsEnumerable().Contains(member.CreatureId))
            .ToArrayAsync(cancellationToken);

        return memberships
            .GroupBy(member => member.CreatureId)
            .ToDictionary(
                group => group.Key,
                IReadOnlyList<Guid> (group) => group.Select(member => member.FactionId).ToArray()
            );
    }
}
