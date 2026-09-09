using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Knowledge.Queries;

public class GetKnownTrapIdsQuery
{
    public required Guid CreatureId { get; init; }
    public required IReadOnlyCollection<Guid> TrapIds { get; init; }
}

internal class GetKnownTrapIdsQueryHandler(IKnowledgeDbContext context)
    : IQueryHandler<GetKnownTrapIdsQuery, IReadOnlySet<Guid>>
{
    public async Task<IReadOnlySet<Guid>> Handle(
        GetKnownTrapIdsQuery query,
        CancellationToken cancellationToken = default
    )
    {
        if (query.TrapIds.Count == 0)
        {
            return new HashSet<Guid>();
        }

        var known = await context
            .CreatureKnowledge.AsNoTracking()
            .Where(knowledge =>
                knowledge.KnowerId == query.CreatureId
                && knowledge.SubjectType == KnowledgeSubjectType.Trap
                && query.TrapIds.AsEnumerable().Contains(knowledge.SubjectId)
            )
            .Select(knowledge => knowledge.SubjectId)
            .ToArrayAsync(cancellationToken);

        return known.ToHashSet();
    }
}
