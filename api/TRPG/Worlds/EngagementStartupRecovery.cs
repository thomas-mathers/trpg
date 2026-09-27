using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TRPG.Application.Common.Clocks;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Queries;
using TRPG.Application.Encounters.Commands;
using TRPG.Application.Worlds.Queries;

namespace TRPG.Worlds;

internal sealed class EngagementStartupRecovery(
    IServiceScopeFactory serviceScopeFactory,
    IWorldClock worldClock,
    ILogger<EngagementStartupRecovery> logger
) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = serviceScopeFactory.CreateAsyncScope();
            var getWorldIds = scope.ServiceProvider.GetRequiredService<
                IQueryHandler<GetWorldIdsQuery, IReadOnlyCollection<Guid>>
            >();
            var cleanup = scope.ServiceProvider.GetRequiredService<
                ICommandHandler<ClearNonEncounterEngagementsCommand>
            >();
            var worldIds = await getWorldIds.Handle(new GetWorldIdsQuery(), cancellationToken);
            foreach (var worldId in worldIds)
            {
                try
                {
                    var gameTime = await worldClock.GetCurrent(worldId, cancellationToken);
                    await cleanup.Handle(
                        new ClearNonEncounterEngagementsCommand
                        {
                            WorldId = worldId,
                            GameTime = gameTime,
                        },
                        cancellationToken
                    );
                }
                catch (Exception exception)
                {
                    LogFailure(exception, worldId);
                }
            }
        }
        catch (Exception exception)
        {
            LogFailure(exception, null);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private void LogFailure(Exception exception, Guid? worldId)
    {
        try
        {
            logger.LogError(
                exception,
                "Failed to recover creature engagements for world {WorldId}",
                worldId
            );
        }
        catch (Exception)
        {
            // Startup recovery must not prevent the host from starting when a logging sink fails.
        }
    }
}
