namespace TRPG.Application.Configuration;

public class QuestChainSeedingOptions
{
    public int MinimumChainLengthBase { get; init; } = 4;
    public int MaximumChainLengthBase { get; init; } = 8;
    public int ChainLengthPerLevel { get; init; } = 1;
    public int ChainLengthCeiling { get; init; } = 32;
}
