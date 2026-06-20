using Microsoft.EntityFrameworkCore;
using TRPG.Data;
using TRPG.Models;

namespace TRPG.Services;

public class LocationService(TrpgDbContext context)
{
    public async Task<World?> GetWorldById(Guid id, CancellationToken cancellationToken = default)
        => await context.Worlds.FindAsync([id], cancellationToken);

    public async Task<Country?> GetCountryById(Guid id, CancellationToken cancellationToken = default)
        => await context.Countries.FindAsync([id], cancellationToken);

    public async Task<List<Country>> GetAllCountriesByWorldId(Guid worldId, CancellationToken cancellationToken = default)
        => await context.Countries
            .Where(c => c.WorldId == worldId)
            .ToListAsync(cancellationToken);

    public async Task<Province?> GetProvinceById(Guid id, CancellationToken cancellationToken = default)
        => await context.Provinces.FindAsync([id], cancellationToken);

    public async Task<List<Province>> GetAllProvincesByCountryId(Guid countryId, CancellationToken cancellationToken = default)
        => await context.Provinces
            .Where(p => p.CountryId == countryId)
            .ToListAsync(cancellationToken);

    public async Task<City?> GetCityById(Guid id, CancellationToken cancellationToken = default)
        => await context.Cities.FindAsync([id], cancellationToken);

    public async Task<List<City>> GetAllCitiesByProvinceId(Guid provinceId, CancellationToken cancellationToken = default)
        => await context.Cities
            .Where(c => c.ProvinceId == provinceId)
            .ToListAsync(cancellationToken);
}
