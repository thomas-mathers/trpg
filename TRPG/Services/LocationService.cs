using Microsoft.EntityFrameworkCore;
using System.Collections.ObjectModel;
using TRPG.Data;
using TRPG.Models;

namespace TRPG.Services;

public class LocationService(TrpgDbContext context)
{
    public async Task<World?> GetWorldById(Guid id, CancellationToken cancellationToken = default)
        => await context.Worlds.FindAsync([id], cancellationToken);

    public async Task<Country?> GetCountryById(Guid id, CancellationToken cancellationToken = default)
        => await context.Countries.FindAsync([id], cancellationToken);

    public async Task<ReadOnlyCollection<Country>> GetAllCountriesByWorldId(Guid worldId, CancellationToken cancellationToken = default)
    {
        var list = await context.Countries
            .Where(c => c.WorldId == worldId)
            .ToListAsync(cancellationToken);
        return list.AsReadOnly();
    }

    public async Task<Province?> GetProvinceById(Guid id, CancellationToken cancellationToken = default)
        => await context.Provinces.FindAsync([id], cancellationToken);

    public async Task<ReadOnlyCollection<Province>> GetAllProvincesByCountryId(Guid countryId, CancellationToken cancellationToken = default)
    {
        var list = await context.Provinces
            .Where(p => p.CountryId == countryId)
            .ToListAsync(cancellationToken);
        return list.AsReadOnly();
    }

    public async Task<City?> GetCityById(Guid id, CancellationToken cancellationToken = default)
        => await context.Cities.FindAsync([id], cancellationToken);

    public async Task<ReadOnlyCollection<City>> GetAllCitiesByProvinceId(Guid provinceId, CancellationToken cancellationToken = default)
    {
        var list = await context.Cities
            .Where(c => c.ProvinceId == provinceId)
            .ToListAsync(cancellationToken);
        return list.AsReadOnly();
    }
}
