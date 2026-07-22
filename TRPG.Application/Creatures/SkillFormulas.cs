namespace TRPG.Application.Creatures;

internal record ExperienceProgress(int Current, int ToNextLevel);

internal static class SkillFormulas
{
    public static int XpForSkillLevel(int level) => 25 * level * (level + 3);

    public static int XpForCharacterLevel(int level) => 100 * level * (level + 5);

    public static ExperienceProgress GetCharacterExperienceProgress(int level, int experience)
    {
        var levelFloor = XpForCharacterLevel(level);
        var nextLevelXp = XpForCharacterLevel(level + 1);
        return new ExperienceProgress(experience - levelFloor, nextLevelXp - levelFloor);
    }
}
