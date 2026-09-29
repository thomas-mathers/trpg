using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Application.Creatures.Queries;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Encounters.Queries;

public class GetLivingHostileCreatureCountsByLocationQuery
{
    public required Guid WorldId { get; init; }
    public required IReadOnlyCollection<Guid> LocationIds { get; init; }
}

internal class GetLivingHostileCreatureCountsByLocationQueryHandler(
    IEncountersDbContext context,
    IQueryHandler<
        GetCreatureConditionsByIdsQuery,
        IReadOnlyDictionary<Guid, CreatureCondition>
    > getCreatureConditionsByIds
) : IQueryHandler<GetLivingHostileCreatureCountsByLocationQuery, IReadOnlyDictionary<Guid, int>>
{
    public async Task<IReadOnlyDictionary<Guid, int>> Handle(
        GetLivingHostileCreatureCountsByLocationQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var groups = await context
            .EncounterGroups.AsNoTracking()
            .Where(group =>
                group.WorldId == query.WorldId
                && query.LocationIds.AsEnumerable().Contains(group.LocationId)
            )
            .Select(group => new { group.Id, group.LocationId })
            .ToArrayAsync(cancellationToken);

        if (groups.Length == 0)
        {
            return new Dictionary<Guid, int>();
        }

        var groupIds = groups.Select(group => group.Id).ToArray();
        var members = await context
            .EncounterGroupMembers.AsNoTracking()
            .Where(member => groupIds.AsEnumerable().Contains(member.EncounterGroupId))
            .Select(member => new { member.EncounterGroupId, member.CreatureId })
            .ToArrayAsync(cancellationToken);

        var conditionsById = await getCreatureConditionsByIds.Handle(
            new GetCreatureConditionsByIdsQuery
            {
                Ids = members.Select(member => member.CreatureId).ToArray(),
            },
            cancellationToken
        );

        var locationIdByGroupId = groups.ToDictionary(group => group.Id, group => group.LocationId);

        return members
            .Where(member =>
                conditionsById.TryGetValue(member.CreatureId, out var state)
                && state != CreatureCondition.Dead
            )
            .GroupBy(member => locationIdByGroupId[member.EncounterGroupId])
            .ToDictionary(group => group.Key, group => group.Count());
    }
}
