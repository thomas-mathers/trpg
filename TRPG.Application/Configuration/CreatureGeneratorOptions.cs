namespace TRPG.Application.Configuration;

public class CreatureGeneratorOptions
{
    public int MaxLevel { get; init; } = 100;
    public int MaxSkillLevel { get; init; } = 100;
    public int PointsPerLevel { get; init; } = 5;
}
