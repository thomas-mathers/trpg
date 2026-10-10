using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TRPG.Application.Common.Clocks;

namespace TRPG.Worlds;

internal sealed class WorldClockCheckpointService(
    IWorldClock worldClock,
    TimeProvider timeProvider,
    ILogger<WorldClockCheckpointService> logger
) : BackgroundService
{
    public static readonly TimeSpan Cadence = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Cadence, timeProvider);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await CheckpointActiveWorlds(stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // The host is shutting down.
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);
        await CheckpointActiveWorlds(cancellationToken);
    }

    private async Task CheckpointActiveWorlds(CancellationToken cancellationToken)
    {
        try
        {
            await worldClock.CheckpointActiveWorlds(cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to checkpoint active world clocks");
        }
    }
}
