using TRPG.Application.Reputations.Mappers;
using TRPG.Domain.Models;

namespace TRPG.Application.GameTurns;

public record NpcReputationMemory(string Text, int OccurrenceCount);

internal static class NpcReputationMemoryCalculator
{
    // What the player did to this person outweighs what they did to a faction it belongs to,
    // without letting a slight eclipse a killing.
    private const int PersonalWeightMultiplier = 2;

    public static IReadOnlyList<NpcReputationMemory> Rank(
        IReadOnlyCollection<ReputationLogEntry> entries,
        int limit
    ) =>
        entries
            .GroupBy(entry => new
            {
                entry.TargetType,
                entry.TargetId,
                entry.Reason,
                entry.Detail,
            })
            .Select(group => new
            {
                Text = group.Key.Detail ?? group.Key.Reason.ToDisplayText(),
                OccurrenceCount = group.Count(),
                Weight = Math.Abs(group.Sum(entry => entry.DeltaScore))
                    * MultiplierFor(group.Key.TargetType),
                LatestOccurredAt = group.Max(entry => entry.CreatedAt),
            })
            .OrderByDescending(memory => memory.Weight)
            .ThenByDescending(memory => memory.LatestOccurredAt)
            .Take(limit)
            .Select(memory => new NpcReputationMemory(memory.Text, memory.OccurrenceCount))
            .ToArray();

    private static int MultiplierFor(ReputationTargetType targetType) =>
        targetType == ReputationTargetType.Creature ? PersonalWeightMultiplier : 1;
}
