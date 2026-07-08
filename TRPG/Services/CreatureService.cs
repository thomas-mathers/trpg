using Microsoft.EntityFrameworkCore;
using TRPG.Data;
using TRPG.Models;

namespace TRPG.Services;

internal record CreatureSummary(
    Guid Id,
    string Name,
    string CreatureTypeName,
    string GenderName,
    string Profession,
    int Level,
    int BirthYear,
    string State);

internal record CreatureLocation(Guid WorldId, Guid? RoomId, Guid StateId, Guid? DistrictId);

internal class CreatureService(TrpgDbContext context) {
    public async Task Add(Creature creature, CancellationToken cancellationToken = default) {
        context.Creatures.Add(creature);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<Creature?> GetById(Guid id, CancellationToken cancellationToken = default) {
        return await context.Creatures.FindAsync([id], cancellationToken);
    }

    public async Task<IReadOnlyCollection<Creature>> GetAllInState(Guid worldId, Guid stateId,
        CancellationToken cancellationToken = default) {
        return await context.Creatures.AsNoTracking()
            .Where(p => p.WorldId == worldId && p.StateId == stateId)
            .ToArrayAsync(cancellationToken);
    }
    
    public async Task<IReadOnlyCollection<CreatureSummary>> GetAllNearby(CreatureLocation location,
        Guid? excludingCreatureId = null, CancellationToken cancellationToken = default) {
        return location.RoomId is { } roomId
            ? await GetAllInRoom(location.WorldId, roomId, excludingCreatureId, cancellationToken)
            : await GetAllOutdoorsInLocation(location.WorldId, location.StateId, location.DistrictId,
                excludingCreatureId, cancellationToken);
    }

    private async Task<IReadOnlyCollection<CreatureSummary>> GetAllInRoom(Guid worldId, Guid roomId,
        Guid? excludingCreatureId = null, CancellationToken cancellationToken = default) {
        var query = context.Creatures.AsNoTracking()
            .Where(p => p.WorldId == worldId && p.RoomId == roomId);
        if (excludingCreatureId is not null) {
            query = query.Where(p => p.Id != excludingCreatureId);
        }

        return await query
            .Select(p => new CreatureSummary(p.Id, p.Name, p.CreatureType.ToString(), p.Gender.ToString(),
                p.Profession.ToString()!, p.Level, p.BirthYear, p.State.ToString()))
            .ToArrayAsync(cancellationToken);
    }

    private async Task<IReadOnlyCollection<CreatureSummary>> GetAllOutdoorsInLocation(Guid worldId, Guid stateId,
        Guid? districtId, Guid? excludingCreatureId = null, CancellationToken cancellationToken = default) {
        var query = context.Creatures.AsNoTracking()
            .Where(p => p.WorldId == worldId && p.StateId == stateId && p.DistrictId == districtId &&
                        p.RoomId == null);
        if (excludingCreatureId is not null) {
            query = query.Where(p => p.Id != excludingCreatureId);
        }

        return await query
            .Select(p => new CreatureSummary(p.Id, p.Name, p.CreatureType.ToString(), p.Gender.ToString(),
                p.Profession.ToString()!, p.Level, p.BirthYear, p.State.ToString()))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Guid>> GetIdsByDistrict(Guid worldId, Guid districtId,
        CancellationToken cancellationToken = default) {
        var ids = await context.Creatures
            .Where(p => p.WorldId == worldId && p.DistrictId == districtId)
            .Select(p => p.Id)
            .ToArrayAsync(cancellationToken);
        return ids;
    }

    public async Task<Creature?> GetByNameInRoom(Guid worldId, Guid roomId, string name,
        CancellationToken cancellationToken = default) {
        return await context.Creatures.AsNoTracking()
            .FirstOrDefaultAsync(p => p.WorldId == worldId && p.RoomId == roomId && p.Name == name, cancellationToken);
    }

    public async Task<Creature?> GetByNameOutdoorsInState(Guid worldId, Guid stateId, string name,
        CancellationToken cancellationToken = default) {
        return await context.Creatures.AsNoTracking()
            .FirstOrDefaultAsync(
                p => p.WorldId == worldId && p.StateId == stateId && p.RoomId == null && p.Name == name,
                cancellationToken);
    }

    public async Task<Creature?> GetByNameOutdoorsInDistrict(Guid worldId, Guid districtId, string name,
        CancellationToken cancellationToken = default) {
        return await context.Creatures.AsNoTracking()
            .FirstOrDefaultAsync(
                p => p.WorldId == worldId && p.DistrictId == districtId && p.RoomId == null && p.Name == name,
                cancellationToken);
    }

    public async Task<Creature?> GetByNameNearby(Guid worldId, Creature player, string name,
        CancellationToken cancellationToken = default) {
        if (player.RoomId is { } roomId) {
            return await GetByNameInRoom(worldId, roomId, name, cancellationToken);
        }

        return player.DistrictId is { } districtId
            ? await GetByNameOutdoorsInDistrict(worldId, districtId, name, cancellationToken)
            : await GetByNameOutdoorsInState(worldId, player.StateId, name, cancellationToken);
    }

    public async Task Update(Creature creature, CancellationToken cancellationToken = default) {
        context.Creatures.Update(creature);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task Delete(Guid id, CancellationToken cancellationToken = default) {
        await context.Creatures.Where(p => p.Id == id).ExecuteDeleteAsync(cancellationToken);
    }
}
