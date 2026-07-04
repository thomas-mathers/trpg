using Microsoft.EntityFrameworkCore;
using TRPG.Data;
using TRPG.Models;

namespace TRPG.Services;

internal class ReputationService(TrpgDbContext context) {
    public async Task AdjustReputation(Guid personId, Guid targetId, ReputationTargetType targetType, int deltaScore,
        CancellationToken cancellationToken = default) {
        var targetExists = targetType == ReputationTargetType.Faction
            ? await context.Factions.AnyAsync(f => f.Id == targetId, cancellationToken)
            : await context.Persons.AnyAsync(p => p.Id == targetId, cancellationToken);

        if (!targetExists) {
            throw new InvalidOperationException($"{targetType} with id {targetId} does not exist.");
        }

        var reputation = await context.Reputations
            .FirstOrDefaultAsync(r => r.PersonId == personId && r.TargetId == targetId && r.TargetType == targetType,
                cancellationToken);

        if (reputation == null) {
            var worldId = await context.Persons
                .Where(p => p.Id == personId)
                .Select(p => p.WorldId)
                .FirstAsync(cancellationToken);
            context.Reputations.Add(new Reputation {
                PersonId = personId, TargetId = targetId, TargetType = targetType, Score = deltaScore, WorldId = worldId
            });
        }
        else {
            reputation.Score += deltaScore;
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<Reputation>> GetAllByPersonId(Guid personId,
        CancellationToken cancellationToken = default) {
        var list = await context.Reputations.Where(r => r.PersonId == personId).ToArrayAsync(cancellationToken);
        return list;
    }

    public async Task<int> GetEffectiveReputation(Guid observerPersonId, Guid targetPersonId,
        CancellationToken cancellationToken = default) {
        var factionIds = await context.FactionMembers
            .Where(fm => fm.PersonId == targetPersonId)
            .Select(fm => fm.FactionId)
            .ToArrayAsync(cancellationToken);

        return await context.Reputations
            .Where(r => r.PersonId == observerPersonId &&
                        ((r.TargetType == ReputationTargetType.Faction && factionIds.Contains(r.TargetId)) ||
                         (r.TargetType == ReputationTargetType.Person && r.TargetId == targetPersonId)))
            .SumAsync(r => r.Score, cancellationToken);
    }
}