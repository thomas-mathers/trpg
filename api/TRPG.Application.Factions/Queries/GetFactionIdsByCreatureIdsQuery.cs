using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data.ModuleContexts;

namespace TRPG.Application.Factions.Queries;

public class GetFactionIdsByCreatureIdsQuery
{
    public required IReadOnlyCollection<Guid> CreatureIds { get; init; }
}

internal class GetFactionIdsByCreatureIdsQueryHandler(IFactionsDbContext context)
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
