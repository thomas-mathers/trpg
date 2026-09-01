using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data;
using TRPG.Domain.Models;

namespace TRPG.Application.Worlds.Queries;

public class GetCapitalCityByCountryIdQuery
{
    public required Guid CountryId { get; init; }
}

internal class GetCapitalCityByCountryIdQueryHandler(TrpgDbContext context)
    : IQueryHandler<GetCapitalCityByCountryIdQuery, City?>
{
    public async Task<City?> Handle(
        GetCapitalCityByCountryIdQuery query,
        CancellationToken cancellationToken = default
    ) =>
        await (
            from city in context.Cities.AsNoTracking()
            join state in context.States.AsNoTracking() on city.StateId equals state.Id
            where state.CountryId == query.CountryId && city.IsCapital
            select city
        ).FirstOrDefaultAsync(cancellationToken);
}
