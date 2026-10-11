using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TRPG.Application.Common.Clocks;
using TRPG.Application.Common.Concurrency;
using TRPG.Application.Scenes.Queries;
using TRPG.Domain;

namespace TRPG.Application.WorldSimulation;

public sealed class WorldSimulationRunnerFactory(
    IServiceScopeFactory serviceScopeFactory,
    IWorldClock worldClock,
    IWorldMutationGate mutationGate,
    TransientCreatureWalkRegistry transientWalks,
    IOptions<WorldSimulationOptions> options,
    ILogger<WorldSimulationRunner> logger
)
{
    public async Task<WorldSimulationRunner> Create(
        Guid worldId,
        GameInstant now,
        CancellationToken cancellationToken = default
    )
    {
        await using var scope = serviceScopeFactory.CreateAsyncScope();
        var loaded = await scope
            .ServiceProvider.GetRequiredService<WorldSimulatorLoader>()
            .Load(worldId, now, cancellationToken);

        var runner = new WorldSimulationRunner(
            worldId,
            loaded,
            options.Value,
            worldClock,
            serviceScopeFactory,
            mutationGate,
            transientWalks,
            logger
        );
        await runner.RefreshWeather(now, cancellationToken);

        return runner;
    }
}
