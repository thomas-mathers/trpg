using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Application.Creatures.Queries;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Encounters.Queries;

public class GetLivingHostileCreatureCountAtLocationsQuery
{
    public required Guid WorldId { get; init; }
    public required IReadOnlyCollection<Guid> LocationIds { get; init; }
}

internal class GetLivingHostileCreatureCountAtLocationsQueryHandler(
    IEncountersDbContext context,
    IQueryHandler<
        GetCreatureStatesByIdsQuery,
        IReadOnlyDictionary<Guid, CreatureState>
    > getCreatureStatesByIds
) : IQueryHandler<GetLivingHostileCreatureCountAtLocationsQuery, int>
{
    public async Task<int> Handle(
        GetLivingHostileCreatureCountAtLocationsQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var groupIds = await context
            .EncounterGroups.AsNoTracking()
            .Where(group =>
                group.WorldId == query.WorldId
                && query.LocationIds.AsEnumerable().Contains(group.LocationId)
            )
            .Select(group => group.Id)
            .ToArrayAsync(cancellationToken);

        if (groupIds.Length == 0)
        {
            return 0;
        }

        var memberCreatureIds = await context
            .EncounterGroupMembers.AsNoTracking()
            .Where(member => groupIds.AsEnumerable().Contains(member.EncounterGroupId))
            .Select(member => member.CreatureId)
            .ToArrayAsync(cancellationToken);

        var statesById = await getCreatureStatesByIds.Handle(
            new GetCreatureStatesByIdsQuery { Ids = memberCreatureIds },
            cancellationToken
        );

        return statesById.Count(kv => kv.Value != CreatureState.Dead);
    }
}
