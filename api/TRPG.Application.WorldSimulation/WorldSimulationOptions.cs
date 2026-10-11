namespace TRPG.Application.WorldSimulation;

public sealed class WorldSimulationOptions
{
    public TimeSpan TickInterval { get; init; } = TimeSpan.FromMilliseconds(200);
    public int RouteSearchesPerTick { get; init; } = 8;
    public TimeSpan ArrivalStagger { get; init; } = TimeSpan.FromMinutes(10);
    public int MessagesPerTick { get; init; } = 32;
    public TimeSpan MessageTimeBudget { get; init; } = TimeSpan.FromMilliseconds(100);
}
