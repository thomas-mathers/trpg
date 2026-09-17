namespace TRPG.Application.Configuration;

public class FactDisclosureOptions
{
    public int SuccessThreshold { get; init; } = 100;
    public int ReputationScoreDivisor { get; init; } = 2;
    public int GoldPerBribeScorePoint { get; init; } = 10;
    public int MinimumLevelAdvantageToIntimidate { get; init; }
    public int IntimidationScorePerLevelAdvantage { get; init; } = 15;
}
