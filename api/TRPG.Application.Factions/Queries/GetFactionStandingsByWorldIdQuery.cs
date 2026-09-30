using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Factions.Queries;

public record GetFactionStandingsByWorldIdQuery(Guid WorldId);

internal class GetFactionStandingsByWorldIdQueryHandler(IFactionsDbContext context)
    : IQueryHandler<GetFactionStandingsByWorldIdQuery, IReadOnlyList<FactionStanding>>
{
    public async Task<IReadOnlyList<FactionStanding>> Handle(
        GetFactionStandingsByWorldIdQuery query,
        CancellationToken cancellationToken = default
    ) =>
        await context
            .FactionStandings.AsNoTracking()
            .Where(standing => standing.WorldId == query.WorldId)
            .ToArrayAsync(cancellationToken);
}
