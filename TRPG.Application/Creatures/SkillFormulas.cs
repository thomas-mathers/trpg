namespace TRPG.Application.Creatures;

internal record ExperienceProgress(int Current, int ToNextLevel);

internal static class SkillFormulas
{
    private const int SkillXpBase = 150;
    private const double SkillXpGrowthRate = 1.05;

    // A skill a creature never invests in (zero archetype affinity) is generated at level 0, below
    // every ability's RequiredSkillLevel floor of 1 - both formulas below clamp to 0 for that case,
    // since the unclamped curves go negative below level 1 and neither call site expects that.
    public static int CalculateSkillExperienceFromSkillLevel(int level) =>
        level <= 0
            ? 0
            : (int)
                Math.Round(
                    SkillXpBase
                        * (Math.Pow(SkillXpGrowthRate, level - 1) - 1)
                        / (SkillXpGrowthRate - 1)
                );

    public static int CalculateExperienceFromSkillLevel(int skillLevel) =>
        skillLevel <= 0 ? 0 : skillLevel * (skillLevel + 1) / 2 - 1;

    public static int CalculateExperienceFromLevel(int level) => level * (level - 1);

    public static int CalculateLevelFromSkillLevels(IReadOnlyList<int> skillLevels)
    {
        var totalExperienceFromSkills = skillLevels.Sum(CalculateExperienceFromSkillLevel);
        var level = 1;
        while (CalculateExperienceFromLevel(level + 1) <= totalExperienceFromSkills)
        {
            level++;
        }
        return level;
    }

    public static ExperienceProgress GetExperienceProgress(int level, int experience)
    {
        var levelFloor = CalculateExperienceFromLevel(level);
        var nextLevelXp = CalculateExperienceFromLevel(level + 1);
        return new ExperienceProgress(experience - levelFloor, nextLevelXp - levelFloor);
    }
}
