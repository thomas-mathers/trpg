using Microsoft.EntityFrameworkCore;
using TRPG.Data;
using TRPG.Data.Models;

namespace TRPG.Application.Worlds.Queries;

internal class GetDistrictByNameInCityQuery
{
    public required Guid CityId { get; init; }
    public required string Name { get; init; }
}

internal class GetDistrictByNameInCityQueryHandler(TrpgDbContext context)
{
    public async Task<District?> Handle(
        GetDistrictByNameInCityQuery query,
        CancellationToken cancellationToken = default
    )
    {
        return await context
            .Districts.AsNoTracking()
            .FirstOrDefaultAsync(
                d => d.CityId == query.CityId && EF.Functions.ILike(d.Name, query.Name),
                cancellationToken
            );
    }
}
