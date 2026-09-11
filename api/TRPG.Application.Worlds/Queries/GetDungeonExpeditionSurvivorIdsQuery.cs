using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data.ModuleContexts;

namespace TRPG.Application.Worlds.Queries;

public record GetDungeonExpeditionSurvivorIdsQuery(
    Guid WorldId,
    IReadOnlyCollection<Guid> CandidateNpcIds
);

internal class GetDungeonExpeditionSurvivorIdsQueryHandler(IWorldsDbContext context)
    : IQueryHandler<GetDungeonExpeditionSurvivorIdsQuery, IReadOnlySet<Guid>>
{
    public async Task<IReadOnlySet<Guid>> Handle(
        GetDungeonExpeditionSurvivorIdsQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var survivorIds = await context
            .DungeonExpeditions.AsNoTracking()
            .Where(expedition =>
                expedition.WorldId == query.WorldId
                && query.CandidateNpcIds.AsEnumerable().Contains(expedition.SurvivorId)
            )
            .Select(expedition => expedition.SurvivorId)
            .ToArrayAsync(cancellationToken);

        return survivorIds.ToHashSet();
    }
}
