namespace TRPG.Application.Configuration;

public class ShakedownOptions
{
    public float TollGoldPerLevel { get; init; } = 5f;
    public int MinimumTollGold { get; init; } = 10;
    public int MaximumTollGold { get; init; } = 200;
}
