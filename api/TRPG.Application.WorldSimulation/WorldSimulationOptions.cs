namespace TRPG.Application.WorldSimulation;

public sealed class WorldSimulationOptions
{
    public TimeSpan TickInterval { get; init; } = TimeSpan.FromMilliseconds(200);
    public int RouteSearchesPerTick { get; init; } = 8;
}
