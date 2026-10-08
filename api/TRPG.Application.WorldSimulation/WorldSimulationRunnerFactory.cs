using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Common.Concurrency;
using TRPG.Domain;

namespace TRPG.Application.WorldSimulation;

public sealed class WorldSimulationRunnerFactory(
    IServiceScopeFactory serviceScopeFactory,
    IWorldMutationGate mutationGate
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

        var runner = new WorldSimulationRunner(worldId, loaded, serviceScopeFactory, mutationGate);
        await runner.RefreshWeather(now, cancellationToken);

        return runner;
    }
}
