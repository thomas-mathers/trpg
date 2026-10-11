namespace TRPG.Application.WorldSimulation;

public sealed record SimulationCadence(
    TimeSpan ActiveLocationPass,
    TimeSpan LocationMaintenance,
    TimeSpan WeatherRefresh
)
{
    public static SimulationCadence Scaled(WorldSimulationOptions options, double timeScale) =>
        new(
            options.ActiveLocationPassInterval * timeScale,
            options.LocationMaintenanceInterval * timeScale,
            options.WeatherRefreshInterval * timeScale
        );
}
