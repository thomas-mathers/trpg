using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TRPG.Application.Common.Clocks;

namespace TRPG.Worlds;

internal sealed class WorldClockCheckpointService(
    IWorldClock worldClock,
    ILogger<WorldClockCheckpointService> logger
) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        try
        {
            await worldClock.CheckpointActiveWorlds(cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to checkpoint active world clocks during shutdown");
        }
    }
}
