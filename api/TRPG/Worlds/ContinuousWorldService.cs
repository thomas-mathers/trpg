using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace TRPG.Worlds;

internal sealed class ContinuousWorldService(
    ContinuousWorldProcessor processor,
    TimeProvider timeProvider,
    ILogger<ContinuousWorldService> logger
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
                await RunPass(stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // The host is shutting down.
        }
    }

    private async Task RunPass(CancellationToken stoppingToken)
    {
        try
        {
            await processor.Process(stoppingToken);
        }
        catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
        {
            logger.LogError(exception, "Continuous world pass failed; retrying on the next tick");
        }
    }
}
