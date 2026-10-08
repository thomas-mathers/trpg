using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using TRPG.Application.Common.Clocks;

namespace TRPG.Application.WorldSimulation;

public sealed class WorldSimulationCoordinator(
    IWorldClock worldClock,
    WorldSimulationRunnerFactory runnerFactory,
    ILogger<WorldSimulationCoordinator> logger
)
{
    private readonly ConcurrentDictionary<Guid, WorldSimulationRunner> _runners = new();
    private readonly ConcurrentDictionary<Guid, byte> _tickingWorlds = new();

    public void Post(Guid worldId, WorldSimulationMessage message)
    {
        if (_runners.TryGetValue(worldId, out var runner))
        {
            runner.Post(message);
        }
    }

    public async Task Tick(CancellationToken cancellationToken = default)
    {
        var activeWorldIds = worldClock.GetActiveWorldIds();
        foreach (var worldId in _runners.Keys.Except(activeWorldIds))
        {
            _runners.TryRemove(worldId, out _);
        }

        await Task.WhenAll(activeWorldIds.Select(worldId => TickWorld(worldId, cancellationToken)));
    }

    private async Task TickWorld(Guid worldId, CancellationToken cancellationToken)
    {
        if (!_tickingWorlds.TryAdd(worldId, 0))
        {
            return;
        }

        try
        {
            var now = await worldClock.GetCurrent(worldId, cancellationToken);
            var runner = await FindOrCreateRunner(worldId, now, cancellationToken);
            await runner.Tick(now, cancellationToken);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogError(exception, "World simulation tick failed for world {WorldId}", worldId);
        }
        finally
        {
            _tickingWorlds.TryRemove(worldId, out _);
        }
    }

    private async Task<WorldSimulationRunner> FindOrCreateRunner(
        Guid worldId,
        TRPG.Domain.GameInstant now,
        CancellationToken cancellationToken
    )
    {
        if (_runners.TryGetValue(worldId, out var existing))
        {
            return existing;
        }

        var created = await runnerFactory.Create(worldId, now, cancellationToken);
        _runners[worldId] = created;
        return created;
    }
}
