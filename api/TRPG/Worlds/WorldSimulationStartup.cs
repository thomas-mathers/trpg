using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TRPG.Application.Common.Clocks;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Queries;
using TRPG.Application.Encounters.Commands;
using TRPG.Application.Worlds.Queries;
using TRPG.Application.WorldSimulation;

namespace TRPG.Worlds;

internal sealed class WorldSimulationStartup(
    IServiceScopeFactory serviceScopeFactory,
    IWorldClock worldClock,
    WorldSimulationCoordinator coordinator,
    ILogger<WorldSimulationStartup> logger
) : IHostedService
{
    private const int MaxConcurrentLoads = 4;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var worldIds = await GetWorldIds(cancellationToken);

        await Parallel.ForEachAsync(
            worldIds,
            new ParallelOptions
            {
                MaxDegreeOfParallelism = MaxConcurrentLoads,
                CancellationToken = cancellationToken,
            },
            LoadRunner
        );

        logger.LogInformation(
            "Loaded simulation runners for {WorldCount} worlds in {ElapsedMs} ms",
            worldIds.Count,
            stopwatch.ElapsedMilliseconds
        );
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task<IReadOnlyCollection<Guid>> GetWorldIds(CancellationToken cancellationToken)
    {
        await using var scope = serviceScopeFactory.CreateAsyncScope();
        var getWorldIds = scope.ServiceProvider.GetRequiredService<
            IQueryHandler<GetWorldIdsQuery, IReadOnlyCollection<Guid>>
        >();

        return await getWorldIds.Handle(new GetWorldIdsQuery(), cancellationToken);
    }

    private async ValueTask LoadRunner(Guid worldId, CancellationToken cancellationToken)
    {
        try
        {
            await ClearAbandonedEngagements(worldId, cancellationToken);
            await coordinator.EnsureRunner(worldId, cancellationToken);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            coordinator.MarkUnavailable(worldId);
            logger.LogError(
                exception,
                "Failed to load the simulation runner of world {WorldId}; the world is unavailable until restart",
                worldId
            );
        }
    }

    private async Task ClearAbandonedEngagements(Guid worldId, CancellationToken cancellationToken)
    {
        var gameTime = await worldClock.GetCurrent(worldId, cancellationToken);

        await using var scope = serviceScopeFactory.CreateAsyncScope();
        var cleanup = scope.ServiceProvider.GetRequiredService<
            ICommandHandler<ClearNonEncounterEngagementsCommand>
        >();
        await cleanup.Handle(
            new ClearNonEncounterEngagementsCommand { WorldId = worldId, GameTime = gameTime },
            cancellationToken
        );
    }
}
