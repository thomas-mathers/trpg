namespace TRPG.Application.WorldSimulation;

public sealed class WorldSimulationOptions
{
    public TimeSpan TickInterval { get; init; } = TimeSpan.FromMilliseconds(200);
    public int RoutinePlansPerTick { get; init; } = 8;
    public TimeSpan ArrivalStagger { get; init; } = TimeSpan.FromMinutes(10);
    public int MessagesPerTick { get; init; } = 32;
    public TimeSpan MessageTimeBudget { get; init; } = TimeSpan.FromMilliseconds(100);
    public TimeSpan ActiveLocationPassInterval { get; init; } = TimeSpan.FromSeconds(5);
    public TimeSpan LocationMaintenanceInterval { get; init; } = TimeSpan.FromSeconds(30);
    public TimeSpan WeatherRefreshInterval { get; init; } = TimeSpan.FromSeconds(30);
    public int ResyncAttempts { get; init; } = 3;
    public TimeSpan ResyncWindow { get; init; } = TimeSpan.FromSeconds(5);
}
