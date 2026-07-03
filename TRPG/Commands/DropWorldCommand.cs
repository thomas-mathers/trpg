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
        var stateIds = await context.States
            .Where(r => countryIds.Contains(r.CountryId)).Select(r => r.Id).ToListAsync(cancellationToken);
        var cityIds = await context.Cities
            .Where(c => stateIds.Contains(c.StateId)).Select(c => c.Id).ToListAsync(cancellationToken);
        var districtIds = await context.Districts
            .Where(d => cityIds.Contains(d.CityId)).Select(d => d.Id).ToListAsync(cancellationToken);
        var buildingIds = await context.Buildings
            .Where(b => stateIds.Contains(b.StateId)).Select(b => b.Id).ToListAsync(cancellationToken);

        var personIds = await context.Persons
            .Where(p => p.WorldId == worldId).Select(p => p.Id).ToListAsync(cancellationToken);

        await context.BuildingOwners.Where(bo => buildingIds.Contains(bo.BuildingId))
            .ExecuteDeleteAsync(cancellationToken);
        await context.PersonAbilities.Where(pa => personIds.Contains(pa.PersonId))
            .ExecuteDeleteAsync(cancellationToken);
        await context.PersonSkills.Where(ps => personIds.Contains(ps.PersonId))
            .ExecuteDeleteAsync(cancellationToken);
        await context.Persons.Where(p => p.WorldId == worldId).ExecuteDeleteAsync(cancellationToken);
        await context.Buildings.Where(b => stateIds.Contains(b.StateId)).ExecuteDeleteAsync(cancellationToken);
        await context.Districts.Where(d => districtIds.Contains(d.Id)).ExecuteDeleteAsync(cancellationToken);
        await context.Roads
            .Where(r => stateIds.Contains(r.OriginStateId) || stateIds.Contains(r.DestinationStateId))
            .ExecuteDeleteAsync(cancellationToken);
        await context.States.Where(r => countryIds.Contains(r.CountryId)).ExecuteDeleteAsync(cancellationToken);
        await context.Countries.Where(c => c.WorldId == worldId).ExecuteDeleteAsync(cancellationToken);
        await context.Races.Where(r => r.WorldId == worldId).ExecuteDeleteAsync(cancellationToken);
        await context.Factions.Where(f => f.WorldId == worldId).ExecuteDeleteAsync(cancellationToken);
        await context.Worlds.Where(w => w.Id == worldId).ExecuteDeleteAsync(cancellationToken);
    }
}