using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Application.Creatures.Queries;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Knowledge.Queries;

public class GetRelativesByCreatureIdsQuery
{
    public required IReadOnlyCollection<Guid> CreatureIds { get; init; }
}

internal class GetRelativesByCreatureIdsQueryHandler(
    IKnowledgeDbContext context,
    IQueryHandler<GetCreaturesByIdsQuery, IReadOnlyDictionary<Guid, Creature>> getCreaturesByIds
)
    : IQueryHandler<
        GetRelativesByCreatureIdsQuery,
        IReadOnlyDictionary<Guid, IReadOnlyList<RelativeSummary>>
    >
{
    public async Task<IReadOnlyDictionary<Guid, IReadOnlyList<RelativeSummary>>> Handle(
        GetRelativesByCreatureIdsQuery query,
        CancellationToken cancellationToken = default
    )
    {
        if (query.CreatureIds.Count == 0)
        {
            return new Dictionary<Guid, IReadOnlyList<RelativeSummary>>();
        }

        var relationships = await context
            .Relationships.AsNoTracking()
            .Where(relationship =>
                query.CreatureIds.AsEnumerable().Contains(relationship.SubjectId)
            )
            .ToArrayAsync(cancellationToken);

        var relativesById = await getCreaturesByIds.Handle(
            new GetCreaturesByIdsQuery
            {
                Ids = relationships.Select(relationship => relationship.RelativeId).ToArray(),
            },
            cancellationToken
        );

        return relationships
            .Where(relationship => relativesById.ContainsKey(relationship.RelativeId))
            .GroupBy(relationship => relationship.SubjectId)
            .ToDictionary(
                group => group.Key,
                IReadOnlyList<RelativeSummary> (group) =>
                    group
                        .Select(relationship => new RelativeSummary(
                            relationship.RelativeId,
                            relativesById[relationship.RelativeId].Name,
                            relationship.RelationshipType
                        ))
                        .ToArray()
            );
    }
}
