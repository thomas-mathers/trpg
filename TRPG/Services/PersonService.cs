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
    
    public async Task<IReadOnlyCollection<Person>> GetAllInRoom(Guid worldId, Guid roomId,
        CancellationToken cancellationToken = default) {
        return await context.Persons
            .Where(p => p.WorldId == worldId && p.RoomId == roomId)
            .ToArrayAsync(cancellationToken);
    }
    
    public async Task<IReadOnlyCollection<Person>> GetAllOutdoorsInRegion(Guid worldId, Guid regionId,
        CancellationToken cancellationToken = default) {
        return await context.Persons
            .Where(p => p.WorldId == worldId && p.RegionId == regionId && p.RoomId == null)
            .ToArrayAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<Person>> GetAllInRegion(Guid worldId, Guid regionId,
        CancellationToken cancellationToken = default) {
        return await context.Persons
            .Where(p => p.WorldId == worldId && p.RegionId == regionId)
            .ToArrayAsync(cancellationToken);
    }

    public async Task<Person?> GetByNameInRoom(Guid worldId, Guid roomId, string name,
        CancellationToken cancellationToken = default) {
        return await context.Persons
            .FirstOrDefaultAsync(p => p.WorldId == worldId && p.RoomId == roomId && p.Name == name, cancellationToken);
    }

    public async Task<Person?> GetByNameOutdoorsInRegion(Guid worldId, Guid regionId, string name,
        CancellationToken cancellationToken = default) {
        return await context.Persons
            .FirstOrDefaultAsync(p => p.WorldId == worldId && p.RegionId == regionId && p.RoomId == null && p.Name == name, cancellationToken);
    }

    public async Task Update(Person person, CancellationToken cancellationToken = default) {
        context.Persons.Update(person);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task Delete(Guid id, CancellationToken cancellationToken = default) {
        await context.Persons.Where(p => p.Id == id).ExecuteDeleteAsync(cancellationToken);
    }
}