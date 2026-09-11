using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Worlds.Queries;

public record GetDungeonExpeditionsQuery(Guid WorldId);

internal class GetDungeonExpeditionsQueryHandler(IWorldsDbContext context)
    : IQueryHandler<GetDungeonExpeditionsQuery, IReadOnlyList<DungeonExpedition>>
{
    public async Task<IReadOnlyList<DungeonExpedition>> Handle(
        GetDungeonExpeditionsQuery query,
        CancellationToken cancellationToken = default
    ) =>
        await context
            .DungeonExpeditions.AsNoTracking()
            .Where(expedition => expedition.WorldId == query.WorldId)
            .ToArrayAsync(cancellationToken);
}
