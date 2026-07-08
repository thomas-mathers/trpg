using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using TRPG.Data;
using TRPG.Models;

namespace TRPG.Services;

internal class LocationService(TrpgDbContext context, IMemoryCache cache) {
    public async Task<World?> GetWorldById(Guid id, CancellationToken cancellationToken = default) {
        return await context.Worlds.FindAsync([id], cancellationToken);
    }

    public async Task<Country?> GetCountryById(Guid id, CancellationToken cancellationToken = default) {
        return await context.Countries.FindAsync([id], cancellationToken);
    }

    public async Task<IReadOnlyCollection<Country>> GetAllCountriesByWorldId(Guid worldId,
        CancellationToken cancellationToken = default) {
        var list = await context.Countries.AsNoTracking()
            .Where(c => c.WorldId == worldId)
            .ToArrayAsync(cancellationToken);
        return list;
    }
    
    public async Task<State?> GetStateById(Guid id, CancellationToken cancellationToken = default) {
        return await cache.GetOrCreateAsync($"state:{id}",
            _ => context.States.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id, cancellationToken));
    }

    public async Task<IReadOnlyCollection<State>> GetAllStatesByCountryId(Guid countryId,
        CancellationToken cancellationToken = default) {
        var list = await context.States.AsNoTracking()
            .Where(r => r.CountryId == countryId)
            .ToArrayAsync(cancellationToken);
        return list;
    }
    
    public async Task<City?> GetCityById(Guid id, CancellationToken cancellationToken = default) {
        return await cache.GetOrCreateAsync($"city:{id}",
            _ => context.Cities.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, cancellationToken));
    }
    
    public async Task<IReadOnlyCollection<District>> GetAllDistrictsByCityId(Guid cityId,
        CancellationToken cancellationToken = default) {
        var districts = await cache.GetOrCreateAsync<District[]>($"districts:{cityId}",
            async _ => await context.Districts.AsNoTracking().Where(d => d.CityId == cityId)
                .ToArrayAsync(cancellationToken));
        return districts ?? [];
    }

    public async Task<District?> GetDistrictByNameInCity(Guid cityId, string name,
        CancellationToken cancellationToken = default) {
        return await context.Districts.AsNoTracking()
            .FirstOrDefaultAsync(d => d.CityId == cityId && EF.Functions.ILike(d.Name, name), cancellationToken);
    }
}