using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Knowledge.Queries;

public record GetKnownFactIdsQuery(Guid WorldId, Guid KnowerId);

internal class GetKnownFactIdsQueryHandler(IKnowledgeDbContext context)
    : IQueryHandler<GetKnownFactIdsQuery, IReadOnlyList<Guid>>
{
    public async Task<IReadOnlyList<Guid>> Handle(
        GetKnownFactIdsQuery query,
        CancellationToken cancellationToken = default
    ) =>
        await context
            .CreatureKnowledge.AsNoTracking()
            .Where(knowledge =>
                knowledge.WorldId == query.WorldId
                && knowledge.KnowerId == query.KnowerId
                && knowledge.SubjectType == KnowledgeSubjectType.Fact
            )
            .Select(knowledge => knowledge.SubjectId)
            .ToArrayAsync(cancellationToken);
}
