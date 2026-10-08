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
    private readonly ConcurrentDictionary<Guid, Lazy<Task<WorldSimulationRunner>>> _runners = new();
    private readonly ConcurrentDictionary<Guid, byte> _tickingWorlds = new();
    private readonly ConcurrentDictionary<Guid, byte> _unavailableWorlds = new();

    public void Post(Guid worldId, WorldSimulationMessage message)
    {
        if (TryGetRunner(worldId, out var runner))
        {
            runner.Post(message);
        }
    }

    public bool IsUnavailable(Guid worldId) => _unavailableWorlds.ContainsKey(worldId);

    public void MarkUnavailable(Guid worldId) => _unavailableWorlds.TryAdd(worldId, 0);

    public async Task EnsureRunner(Guid worldId, CancellationToken cancellationToken = default)
    {
        var loading = _runners.GetOrAdd(
            worldId,
            id => new Lazy<Task<WorldSimulationRunner>>(() => LoadRunner(id))
        );

        try
        {
            await loading.Value.WaitAsync(cancellationToken);
        }
        catch (Exception) when (loading.Value.IsFaulted)
        {
            _runners.TryRemove(KeyValuePair.Create(worldId, loading));
            MarkUnavailable(worldId);
            throw;
        }
    }

    public async Task Tick(CancellationToken cancellationToken = default)
    {
        var activeWorldIds = worldClock.GetActiveWorldIds();
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
            if (!TryGetRunner(worldId, out var runner))
            {
                return;
            }

            var now = await worldClock.GetCurrent(worldId, cancellationToken);
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

    private bool TryGetRunner(Guid worldId, out WorldSimulationRunner runner)
    {
        if (
            _runners.TryGetValue(worldId, out var loading)
            && loading.Value is { IsCompletedSuccessfully: true } loaded
        )
        {
            runner = loaded.Result;
            return true;
        }

        runner = null!;
        return false;
    }

    private async Task<WorldSimulationRunner> LoadRunner(Guid worldId)
    {
        var now = await worldClock.GetCurrent(worldId);
        return await runnerFactory.Create(worldId, now);
    }
}
