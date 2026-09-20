using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Factions.Queries;

public class GetCityFactionByCityIdQuery
{
    public required Guid CityId { get; init; }
}

internal class GetCityFactionByCityIdQueryHandler(IFactionsDbContext context)
    : IQueryHandler<GetCityFactionByCityIdQuery, Guid?>
{
    public Task<Guid?> Handle(
        GetCityFactionByCityIdQuery query,
        CancellationToken cancellationToken = default
    ) =>
        context
            .Factions.AsNoTracking()
            .Where(faction =>
                faction.IsCityFaction
                && faction.CityId == query.CityId
                && (faction.Kind == FactionKind.People || faction.Kind == FactionKind.Unclassified)
            )
            .Select(faction => (Guid?)faction.Id)
            .FirstOrDefaultAsync(cancellationToken);
}
