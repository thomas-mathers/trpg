using Microsoft.EntityFrameworkCore;
using TRPG.Data;
using TRPG.Models;

namespace TRPG.Services;

internal class ReputationService(TrpgDbContext context) {
    public async Task AdjustReputation(Guid creatureId, Guid targetId, ReputationTargetType targetType, int deltaScore,
        CancellationToken cancellationToken = default) {
        var targetExists = targetType == ReputationTargetType.Faction
            ? await context.Factions.AnyAsync(f => f.Id == targetId, cancellationToken)
            : await context.Creatures.AnyAsync(p => p.Id == targetId, cancellationToken);

        if (!targetExists) {
            throw new InvalidOperationException($"{targetType} with id {targetId} does not exist.");
        }

        var reputation = await context.Reputations
            .FirstOrDefaultAsync(r => r.CreatureId == creatureId && r.TargetId == targetId && r.TargetType == targetType,
                cancellationToken);

        if (reputation == null) {
            var worldId = await context.Creatures
                .Where(p => p.Id == creatureId)
                .Select(p => p.WorldId)
                .FirstAsync(cancellationToken);
            context.Reputations.Add(new Reputation {
                CreatureId = creatureId, TargetId = targetId, TargetType = targetType, Score = deltaScore,
                WorldId = worldId
            });
        }
        else {
            reputation.Score += deltaScore;
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<Reputation>> GetAllByCreatureId(Guid creatureId,
        CancellationToken cancellationToken = default) {
        var list = await context.Reputations.AsNoTracking().Where(r => r.CreatureId == creatureId)
            .ToArrayAsync(cancellationToken);
        return list;
    }

    public async Task<int> GetEffectiveReputation(Guid observerCreatureId, Guid targetCreatureId,
        CancellationToken cancellationToken = default) {
        var factionIds = await context.FactionMembers
            .Where(fm => fm.CreatureId == targetCreatureId)
            .Select(fm => fm.FactionId)
            .ToArrayAsync(cancellationToken);

        return await context.Reputations
            .Where(r => r.CreatureId == observerCreatureId &&
                        ((r.TargetType == ReputationTargetType.Faction && factionIds.Contains(r.TargetId)) ||
                         (r.TargetType == ReputationTargetType.Creature && r.TargetId == targetCreatureId)))
            .SumAsync(r => r.Score, cancellationToken);
    }
}
