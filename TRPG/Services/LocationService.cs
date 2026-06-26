using System.Collections.ObjectModel;
using Microsoft.EntityFrameworkCore;
using TRPG.Data;
using TRPG.Models;

namespace TRPG.Services;

internal class LocationService(TrpgDbContext context) {
    public async Task<World?> GetWorldById(Guid id, CancellationToken cancellationToken = default) {
        return await context.Worlds.FindAsync([id], cancellationToken);
    }

    public async Task<Country?> GetCountryById(Guid id, CancellationToken cancellationToken = default) {
        return await context.Countries.FindAsync([id], cancellationToken);
    }

    public async Task<ReadOnlyCollection<Country>> GetAllCountriesByWorldId(Guid worldId,
        CancellationToken cancellationToken = default) {
        var list = await context.Countries
            .Where(c => c.WorldId == worldId)
            .ToListAsync(cancellationToken);
        return list.AsReadOnly();
    }

    public async Task<City?> GetCityById(Guid id, CancellationToken cancellationToken = default) {
        return await context.Cities.FindAsync([id], cancellationToken);
    }

    public async Task<ReadOnlyCollection<City>> GetAllCitiesByCountryId(Guid countryId,
        CancellationToken cancellationToken = default) {
        var list = await context.Cities
            .Where(c => c.CountryId == countryId)
            .ToListAsync(cancellationToken);
        return list.AsReadOnly();
    }
}
