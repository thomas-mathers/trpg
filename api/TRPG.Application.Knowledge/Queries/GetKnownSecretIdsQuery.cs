using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Knowledge.Queries;

public record GetKnownSecretIdsQuery(Guid WorldId, Guid KnowerId);

internal class GetKnownSecretIdsQueryHandler(IKnowledgeDbContext context)
    : IQueryHandler<GetKnownSecretIdsQuery, IReadOnlyList<Guid>>
{
    public async Task<IReadOnlyList<Guid>> Handle(
        GetKnownSecretIdsQuery query,
        CancellationToken cancellationToken = default
    ) =>
        await context
            .CreatureKnowledge.AsNoTracking()
            .Where(knowledge =>
                knowledge.WorldId == query.WorldId
                && knowledge.KnowerId == query.KnowerId
                && knowledge.SubjectType == KnowledgeSubjectType.Secret
            )
            .Select(knowledge => knowledge.SubjectId)
            .ToArrayAsync(cancellationToken);
}
