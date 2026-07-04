using Microsoft.EntityFrameworkCore;
using TRPG.Data;
using TRPG.Models;

namespace TRPG.Services;

internal class FactionService(TrpgDbContext context) {
    public async Task AddMember(Guid factionId, Guid personId, FactionRole role,
        CancellationToken cancellationToken = default) {
        var worldId = await context.Persons
            .Where(p => p.Id == personId)
            .Select(p => p.WorldId)
            .FirstAsync(cancellationToken);

        context.FactionMembers.Add(new FactionMember {
            Id = Guid.NewGuid(),
            FactionId = factionId,
            PersonId = personId,
            Role = role,
            WorldId = worldId
        });

        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<Faction?> GetById(Guid id, CancellationToken cancellationToken = default) {
        return await context.Factions.FindAsync([id], cancellationToken);
    }

    public async Task<IReadOnlyCollection<FactionMember>> GetAllMembershipsByPersonId(Guid personId,
        CancellationToken cancellationToken = default) {
        var list = await context.FactionMembers
            .Where(fm => fm.PersonId == personId)
            .ToArrayAsync(cancellationToken);
        return list;
    }

    public async Task<IReadOnlyCollection<FactionMember>> GetAllMembersByFactionId(Guid factionId,
        CancellationToken cancellationToken = default) {
        var list = await context.FactionMembers
            .Where(fm => fm.FactionId == factionId)
            .ToArrayAsync(cancellationToken);
        return list;
    }

    public async Task UpdateMemberRole(Guid factionId, Guid memberId, FactionRole role,
        CancellationToken cancellationToken = default) {
        var member = await context.FactionMembers
            .FirstOrDefaultAsync(fm => fm.FactionId == factionId && fm.PersonId == memberId, cancellationToken);

        if (member is null) {
            throw new InvalidOperationException($"Person {memberId} is not a member of faction {factionId}.");
        }

        member.Role = role;

        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveMember(Guid factionId, Guid memberId, CancellationToken cancellationToken = default) {
        await context.FactionMembers
            .Where(fm => fm.FactionId == factionId && fm.PersonId == memberId)
            .ExecuteDeleteAsync(cancellationToken);
    }
}