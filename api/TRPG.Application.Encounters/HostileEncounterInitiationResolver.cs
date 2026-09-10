namespace TRPG.Application.Encounters;

using TRPG.Application.Creatures;

internal record HostileEncounterCandidateGroup(
    Guid GroupId,
    int Aggression,
    int ReputationSensitivity,
    int RiskAversion,
    int ReputationScore,
    IReadOnlyList<int> LivingMemberLevels
);

internal static class HostileEncounterInitiationResolver
{
    private const double EngagementThreshold = 100.0;

    public static Guid? Resolve(
        int playerLevel,
        IReadOnlyList<HostileEncounterCandidateGroup> candidates,
        IChanceRoller chanceRoller
    )
    {
        var strongestCandidate = candidates
            .Where(candidate => candidate.LivingMemberLevels.Count > 0)
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

    private static double EngagementScore(int playerLevel, HostileEncounterCandidateGroup candidate)
    {
        var groupPower = candidate.LivingMemberLevels.Sum();
        var strengthAdvantageFactor = (groupPower - playerLevel) / (double)playerLevel;
        var reputationFactor =
            1 - candidate.ReputationScore / 100.0 * candidate.ReputationSensitivity / 100.0;
        var confidenceFactor = 1 + strengthAdvantageFactor * (1 - candidate.RiskAversion / 100.0);

        return candidate.Aggression * reputationFactor * confidenceFactor;
    }
}
