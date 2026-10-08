using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using TRPG.Application.WorldSimulation;

namespace TRPG.Worlds;

internal sealed class WorldSimulationService(
    WorldSimulationCoordinator coordinator,
    IOptions<WorldSimulationOptions> options,
    TimeProvider timeProvider
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(options.Value.TickInterval, timeProvider);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await coordinator.Tick(stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // The host is shutting down.
        }
    }
}
