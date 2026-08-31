using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data;
using TRPG.Domain.Models;

namespace TRPG.Application.Creatures.Queries;

public class GetRelativesQuery
{
    public required Guid CreatureId { get; init; }
}

public record RelativeSummary(Guid RelativeId, string Name, RelationshipType RelationshipType);

internal class GetRelativesQueryHandler(TrpgDbContext context)
    : IQueryHandler<GetRelativesQuery, IReadOnlyCollection<RelativeSummary>>
{
    public async Task<IReadOnlyCollection<RelativeSummary>> Handle(
        GetRelativesQuery query,
        CancellationToken cancellationToken = default
    ) =>
        await (
            from relationship in context.Relationships.AsNoTracking()
            where relationship.SubjectId == query.CreatureId
            join relative in context.Creatures.AsNoTracking()
                on relationship.RelativeId equals relative.Id
            select new RelativeSummary(relative.Id, relative.Name, relationship.RelationshipType)
        ).ToArrayAsync(cancellationToken);
}
