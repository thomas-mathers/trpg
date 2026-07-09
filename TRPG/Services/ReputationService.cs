using Microsoft.EntityFrameworkCore;
using TRPG.Data;
using TRPG.Data.Models;

namespace TRPG.Services;

internal class ReputationService(TrpgDbContext context)
{
    public async Task AdjustReputation(
        Guid creatureId,
        Guid targetId,
        ReputationTargetType targetType,
        int deltaScore,
        CancellationToken cancellationToken = default
    )
    {
        var targetExists =
            targetType == ReputationTargetType.Faction
                ? await context.Factions.AnyAsync(f => f.Id == targetId, cancellationToken)
                : await context.Creatures.AnyAsync(p => p.Id == targetId, cancellationToken);

        if (!targetExists)
        {
            throw new InvalidOperationException($"{targetType} with id {targetId} does not exist.");
        }

        var reputation = await context.Reputations.FirstOrDefaultAsync(
            r => r.CreatureId == creatureId && r.TargetId == targetId && r.TargetType == targetType,
            cancellationToken
        );

        if (reputation == null)
        {
            var worldId = await context
                .Creatures.Where(p => p.Id == creatureId)
                .Select(p => p.WorldId)
                .FirstAsync(cancellationToken);
            context.Reputations.Add(
                new Reputation
                {
                    CreatureId = creatureId,
                    TargetId = targetId,
                    TargetType = targetType,
                    Score = deltaScore,
                    WorldId = worldId,
                }
            );
        }
        else
        {
            reputation.Score += deltaScore;
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<Reputation>> GetAllByCreatureId(
        Guid creatureId,
        CancellationToken cancellationToken = default
    )
    {
        var list = await context
            .Reputations.AsNoTracking()
            .Where(r => r.CreatureId == creatureId)
            .ToArrayAsync(cancellationToken);
        return list;
    }

    public async Task<int> GetEffectiveReputation(
        Guid observerCreatureId,
        Guid targetCreatureId,
        CancellationToken cancellationToken = default
    )
    {
        var factionIds = await context
            .FactionMembers.Where(fm => fm.CreatureId == targetCreatureId)
            .Select(fm => fm.FactionId)
            .ToArrayAsync(cancellationToken);
        var factionIdsByCreature = new Dictionary<Guid, Guid[]> { [targetCreatureId] = factionIds };

        var reputations = await GetEffectiveReputations(
            observerCreatureId,
            [targetCreatureId],
            factionIdsByCreature,
            cancellationToken
        );
        return reputations.GetValueOrDefault(targetCreatureId, 0);
    }

    public async Task<IReadOnlyDictionary<Guid, int>> GetEffectiveReputations(
        Guid observerCreatureId,
        IReadOnlyCollection<Guid> targetCreatureIds,
        IReadOnlyDictionary<Guid, Guid[]> factionIdsByCreature,
        CancellationToken cancellationToken = default
    )
    {
        if (targetCreatureIds.Count == 0)
        {
            return new Dictionary<Guid, int>();
        }

        var allFactionIds = targetCreatureIds
            .SelectMany(id => factionIdsByCreature.GetValueOrDefault(id, []))
            .Distinct()
            .ToArray();

        var reputations = await context
            .Reputations.Where(r =>
                r.CreatureId == observerCreatureId
                && (
                    (
                        r.TargetType == ReputationTargetType.Faction
                        && allFactionIds.Contains(r.TargetId)
                    )
                    || (
                        r.TargetType == ReputationTargetType.Creature
                        && targetCreatureIds.Contains(r.TargetId)
                    )
                )
            )
            .ToArrayAsync(cancellationToken);

        var reputationByFactionId = reputations
            .Where(r => r.TargetType == ReputationTargetType.Faction)
            .ToDictionary(r => r.TargetId, r => r.Score);
        var reputationByCreatureId = reputations
            .Where(r => r.TargetType == ReputationTargetType.Creature)
            .ToDictionary(r => r.TargetId, r => r.Score);

        return targetCreatureIds.ToDictionary(
            id => id,
            id =>
            {
                var directScore = reputationByCreatureId.GetValueOrDefault(id, 0);
                var factionScore = factionIdsByCreature
                    .GetValueOrDefault(id, [])
                    .Sum(factionId => reputationByFactionId.GetValueOrDefault(factionId, 0));
                return directScore + factionScore;
            }
        );
    }
}
