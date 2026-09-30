using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Factions.Queries;

public record GetFactionsByWorldIdQuery(Guid WorldId);

internal class GetFactionsByWorldIdQueryHandler(IFactionsDbContext context)
    : IQueryHandler<GetFactionsByWorldIdQuery, IReadOnlyList<Faction>>
{
    public async Task<IReadOnlyList<Faction>> Handle(
        GetFactionsByWorldIdQuery query,
        CancellationToken cancellationToken = default
    ) =>
        await context
            .Factions.AsNoTracking()
            .Where(faction => faction.WorldId == query.WorldId)
            .ToArrayAsync(cancellationToken);
}
