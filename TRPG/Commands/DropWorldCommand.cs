using Microsoft.EntityFrameworkCore;
using TRPG.Data;

namespace TRPG.Commands;

internal class DropWorldCommand {
    public required Guid WorldId { get; init; }
}

internal class DropWorldCommandHandler(TrpgDbContext context) {
    public async Task Handle(DropWorldCommand command, CancellationToken cancellationToken = default) {
        var worldId = command.WorldId;

        var countryIds = await context.Countries
            .Where(c => c.WorldId == worldId).Select(c => c.Id).ToListAsync(cancellationToken);
        var cityIds = await context.Cities
            .Where(c => countryIds.Contains(c.CountryId)).Select(c => c.Id).ToListAsync(cancellationToken);
        var buildingIds = await context.Buildings
            .Where(b => cityIds.Contains(b.CityId)).Select(b => b.Id).ToListAsync(cancellationToken);
        var skillIds = await context.Skills
            .Where(s => s.WorldId == worldId).Select(s => s.Id).ToListAsync(cancellationToken);

        await context.SkillPrerequisites.Where(sp => skillIds.Contains(sp.SkillId))
            .ExecuteDeleteAsync(cancellationToken);
        await context.BuildingOwners.Where(bo => buildingIds.Contains(bo.BuildingId))
            .ExecuteDeleteAsync(cancellationToken);
        await context.Persons.Where(p => p.WorldId == worldId).ExecuteDeleteAsync(cancellationToken);
        await context.Buildings.Where(b => cityIds.Contains(b.CityId)).ExecuteDeleteAsync(cancellationToken);
        await context.TravelRoutes
            .Where(tr => cityIds.Contains(tr.OriginCityId) || cityIds.Contains(tr.DestinationCityId))
            .ExecuteDeleteAsync(cancellationToken);
        await context.Cities.Where(c => countryIds.Contains(c.CountryId)).ExecuteDeleteAsync(cancellationToken);
        await context.Countries.Where(c => c.WorldId == worldId).ExecuteDeleteAsync(cancellationToken);
        await context.Skills.Where(s => s.WorldId == worldId).ExecuteDeleteAsync(cancellationToken);
        await context.Races.Where(r => r.WorldId == worldId).ExecuteDeleteAsync(cancellationToken);
        await context.Professions.Where(p => p.WorldId == worldId).ExecuteDeleteAsync(cancellationToken);
        await context.Factions.Where(f => f.WorldId == worldId).ExecuteDeleteAsync(cancellationToken);
        await context.Worlds.Where(w => w.Id == worldId).ExecuteDeleteAsync(cancellationToken);
    }
}
