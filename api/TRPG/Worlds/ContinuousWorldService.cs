using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace TRPG.Worlds;

internal sealed class ContinuousWorldService(
    ContinuousWorldProcessor processor,
    TimeProvider timeProvider,
    ILogger<ContinuousWorldService> logger
) : BackgroundService
{
    public static readonly TimeSpan FrequentCadence = TimeSpan.FromSeconds(5);
    public static readonly TimeSpan RoutineCadence = TimeSpan.FromSeconds(30);

    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        Task.WhenAll(
            RunEvery(FrequentCadence, processor.ProcessFrequent, stoppingToken),
            RunEvery(RoutineCadence, processor.ProcessRoutines, stoppingToken)
        );

    private async Task RunEvery(
        TimeSpan cadence,
        Func<CancellationToken, Task> pass,
        CancellationToken stoppingToken
    )
    {
        using var timer = new PeriodicTimer(cadence, timeProvider);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await RunPass(pass, stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // The host is shutting down.
        }
    }

    private async Task RunPass(Func<CancellationToken, Task> pass, CancellationToken stoppingToken)
    {
        try
        {
            await pass(stoppingToken);
        }
        catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
        {
            logger.LogError(exception, "Continuous world pass failed; retrying on the next tick");
        }
    }
}
