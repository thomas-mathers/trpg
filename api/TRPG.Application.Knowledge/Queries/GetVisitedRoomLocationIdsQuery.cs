using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Knowledge.Queries;

public class GetVisitedRoomLocationIdsQuery
{
    public required Guid CreatureId { get; init; }
    public required IReadOnlyCollection<Guid> RoomLocationIds { get; init; }
}

internal class GetVisitedRoomLocationIdsQueryHandler(IKnowledgeDbContext context)
    : IQueryHandler<GetVisitedRoomLocationIdsQuery, IReadOnlySet<Guid>>
{
    public async Task<IReadOnlySet<Guid>> Handle(
        GetVisitedRoomLocationIdsQuery query,
        CancellationToken cancellationToken = default
    )
    {
        if (query.RoomLocationIds.Count == 0)
        {
            return new HashSet<Guid>();
        }

        var visited = await context
            .CreatureKnowledge.AsNoTracking()
            .Where(knowledge =>
                knowledge.KnowerId == query.CreatureId
                && knowledge.SubjectType == KnowledgeSubjectType.Room
                && query.RoomLocationIds.AsEnumerable().Contains(knowledge.SubjectId)
            )
            .Select(knowledge => knowledge.SubjectId)
            .ToArrayAsync(cancellationToken);

        return visited.ToHashSet();
    }
}
