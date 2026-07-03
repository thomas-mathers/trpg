using Microsoft.EntityFrameworkCore;
using TRPG.Data;
using TRPG.Models;

namespace TRPG.Services;

internal class PersonService(TrpgDbContext context) {
    public async Task Add(Person person, CancellationToken cancellationToken = default) {
        context.Persons.Add(person);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<Person?> GetById(Guid id, CancellationToken cancellationToken = default) {
        return await context.Persons.FindAsync([id], cancellationToken);
    }
    
    public async Task<IReadOnlyCollection<Person>> GetAllInState(Guid worldId, Guid stateId,
        CancellationToken cancellationToken = default) {
        return await context.Persons
            .Where(p => p.WorldId == worldId && p.StateId == stateId)
            .ToArrayAsync(cancellationToken);
    }

    public async Task<Person?> GetByNameInRoom(Guid worldId, Guid roomId, string name,
        CancellationToken cancellationToken = default) {
        return await context.Persons
            .FirstOrDefaultAsync(p => p.WorldId == worldId && p.RoomId == roomId && p.Name == name, cancellationToken);
    }

    public async Task<Person?> GetByNameOutdoorsInState(Guid worldId, Guid stateId, string name,
        CancellationToken cancellationToken = default) {
        return await context.Persons
            .FirstOrDefaultAsync(p => p.WorldId == worldId && p.StateId == stateId && p.RoomId == null && p.Name == name, cancellationToken);
    }

    public async Task<Person?> GetByNameOutdoorsInDistrict(Guid worldId, Guid districtId, string name,
        CancellationToken cancellationToken = default) {
        return await context.Persons
            .FirstOrDefaultAsync(p => p.WorldId == worldId && p.DistrictId == districtId && p.RoomId == null && p.Name == name, cancellationToken);
    }

    public async Task<Person?> GetByNameNearby(Guid worldId, Person player, string name,
        CancellationToken cancellationToken = default) {
        if (player.RoomId is { } roomId) {
            return await GetByNameInRoom(worldId, roomId, name, cancellationToken);
        }

        return player.DistrictId is { } districtId
            ? await GetByNameOutdoorsInDistrict(worldId, districtId, name, cancellationToken)
            : await GetByNameOutdoorsInState(worldId, player.StateId, name, cancellationToken);
    }

    public async Task Update(Person person, CancellationToken cancellationToken = default) {
        context.Persons.Update(person);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task Delete(Guid id, CancellationToken cancellationToken = default) {
        await context.Persons.Where(p => p.Id == id).ExecuteDeleteAsync(cancellationToken);
    }
}