using TRPG.Application.Configuration;

namespace TRPG.Application.Quests;

internal static class FactDisclosureScoreCalculator
{
    internal static bool CanAttemptIntimidation(
        int playerLevel,
        int npcLevel,
        FactDisclosureOptions options
    ) => playerLevel - npcLevel >= options.MinimumLevelAdvantageToIntimidate;

    internal static int Score(
        int baseWillingness,
        int effectiveReputation,
        int approachContribution,
        int completedWeightedSupportingQuestTotal,
        FactDisclosureOptions options
    ) =>
        baseWillingness
        + effectiveReputation / options.ReputationScoreDivisor
        + approachContribution
        + completedWeightedSupportingQuestTotal;

    internal static bool Succeeds(int score, FactDisclosureOptions options) =>
        score >= options.SuccessThreshold;
}
