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

    public async Task<IReadOnlyCollection<Country>> GetAllCountriesByWorldId(Guid worldId,
        CancellationToken cancellationToken = default) {
        var list = await context.Countries
            .Where(c => c.WorldId == worldId)
            .ToArrayAsync(cancellationToken);
        return list;
    }

    public async Task<State?> GetStateById(Guid id, CancellationToken cancellationToken = default) {
        return await context.States.FindAsync([id], cancellationToken);
    }

    public async Task<IReadOnlyCollection<State>> GetAllStatesByCountryId(Guid countryId,
        CancellationToken cancellationToken = default) {
        var list = await context.States
            .Where(r => r.CountryId == countryId)
            .ToArrayAsync(cancellationToken);
        return list;
    }

    public async Task<City?> GetCityById(Guid id, CancellationToken cancellationToken = default) {
        return await context.Cities.FindAsync([id], cancellationToken);
    }

    public async Task<IReadOnlyCollection<City>> GetAllCitiesByStateId(Guid stateId,
        CancellationToken cancellationToken = default) {
        var list = await context.Cities
            .Where(c => c.StateId == stateId)
            .ToArrayAsync(cancellationToken);
        return list;
    }

    public async Task<District?> GetDistrictById(Guid id, CancellationToken cancellationToken = default) {
        return await context.Districts.FindAsync([id], cancellationToken);
    }

    public async Task<IReadOnlyCollection<District>> GetAllDistrictsByCityId(Guid cityId,
        CancellationToken cancellationToken = default) {
        var list = await context.Districts
            .Where(d => d.CityId == cityId)
            .ToArrayAsync(cancellationToken);
        return list;
    }

    public async Task<District?> GetDistrictByNameInCity(Guid cityId, string name,
        CancellationToken cancellationToken = default) {
        return await context.Districts
            .FirstOrDefaultAsync(d => d.CityId == cityId && EF.Functions.ILike(d.Name, name), cancellationToken);
    }
}