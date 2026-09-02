using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Application.Creatures.Queries;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Knowledge.Queries;

public class GetRelativesQuery
{
    public required Guid CreatureId { get; init; }
}

public record RelativeSummary(Guid RelativeId, string Name, RelationshipType RelationshipType);

internal class GetRelativesQueryHandler(
    IKnowledgeDbContext context,
    IQueryHandler<GetCreaturesByIdsQuery, IReadOnlyDictionary<Guid, Creature>> getCreaturesByIds
) : IQueryHandler<GetRelativesQuery, IReadOnlyCollection<RelativeSummary>>
{
    public async Task<IReadOnlyCollection<RelativeSummary>> Handle(
        GetRelativesQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var relationships = await context
            .Relationships.AsNoTracking()
            .Where(relationship => relationship.SubjectId == query.CreatureId)
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
            .Select(relationship => new RelativeSummary(
                relationship.RelativeId,
                relativesById[relationship.RelativeId].Name,
                relationship.RelationshipType
            ))
            .ToArray();
    }
}
