using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data;

namespace TRPG.Application.Factions.Queries;

public class GetNonCityFactionNamesByCreatureIdQuery
{
    public required Guid CreatureId { get; init; }
}

internal class GetNonCityFactionNamesByCreatureIdQueryHandler(TrpgDbContext context)
    : IQueryHandler<GetNonCityFactionNamesByCreatureIdQuery, IReadOnlyList<string>>
{
    public async Task<IReadOnlyList<string>> Handle(
        GetNonCityFactionNamesByCreatureIdQuery query,
        CancellationToken cancellationToken = default
    ) =>
        await (
            from fm in context.FactionMembers.AsNoTracking()
            where fm.CreatureId == query.CreatureId
            join f in context.Factions.AsNoTracking() on fm.FactionId equals f.Id
            where !f.IsCityFaction
            select f.Name
        ).ToArrayAsync(cancellationToken);
}
