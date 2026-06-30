namespace TRPG;

internal static class GameRules {
    public const int MaxLevel = 100;
    public const int MaxSkillLevel = 100;
    public const int PointsPerLevel = 5;

    public static int XpForSkillLevel(int level) => 25 * level * (level + 3);
}