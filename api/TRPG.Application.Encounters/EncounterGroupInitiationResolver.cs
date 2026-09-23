namespace TRPG.Application.Encounters;

using TRPG.Application.Creatures;
using TRPG.Domain.Models;

internal record EncounterGroupCandidate(
    Guid GroupId,
    EncounterApproach Approach,
    int Aggression,
    int ReputationSensitivity,
    int RiskAversion,
    int ReputationScore,
    IReadOnlyList<int> LivingMemberLevels
);

internal static class EncounterGroupInitiationResolver
{
    private const double EngagementThreshold = 100.0;

    public static Guid? Resolve(
        int playerLevel,
        IReadOnlyList<EncounterGroupCandidate> candidates,
        IChanceRoller chanceRoller
    )
    {
        var eligibleCandidates = candidates
            .Where(candidate =>
                candidate.Approach != EncounterApproach.None
                && candidate.LivingMemberLevels.Count > 0
                && candidate.Aggression > 0
            )
            .ToArray();
        var guaranteedCandidate = eligibleCandidates
            .Where(candidate => candidate.Aggression == 100)
            .OrderByDescending(candidate => candidate.LivingMemberLevels.Sum())
            .ThenBy(candidate => candidate.GroupId)
            .FirstOrDefault();
        if (guaranteedCandidate != null)
        {
            return guaranteedCandidate.GroupId;
        }

        var strongestCandidate = eligibleCandidates
            .Select(candidate =>
                (candidate.GroupId, Score: EngagementScore(playerLevel, candidate))
            )
            .OrderByDescending(candidate => candidate.Score)
            .FirstOrDefault();
        if (strongestCandidate == default)
        {
            return null;
        }

        var engagementChance = (float)
            Math.Clamp(strongestCandidate.Score / EngagementThreshold, 0.0, 1.0);
        if (engagementChance == 0)
        {
            return null;
        }

        return chanceRoller.Roll(engagementChance) ? strongestCandidate.GroupId : null;
    }

    private static double EngagementScore(int playerLevel, EncounterGroupCandidate candidate)
    {
        var groupPower = candidate.LivingMemberLevels.Sum();
        var strengthAdvantageFactor = (groupPower - playerLevel) / (double)playerLevel;
        var reputationFactor =
            1 - candidate.ReputationScore / 100.0 * candidate.ReputationSensitivity / 100.0;
        var confidenceFactor = 1 + strengthAdvantageFactor * (1 - candidate.RiskAversion / 100.0);

        return candidate.Aggression * reputationFactor * confidenceFactor;
    }
}
