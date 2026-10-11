using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using TRPG.Application.Common.Clocks;
using TRPG.Application.WorldSimulation.Poses;

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

    public Task<T> Request<T>(
        Guid worldId,
        Func<LivePlayerPoses, CancellationToken, Task<T>> work,
        CancellationToken cancellationToken
    )
    {
        if (!TryGetRunner(worldId, out var runner))
        {
            return work(new LivePlayerPoses(), cancellationToken);
        }

        var completion = new TaskCompletionSource<T>(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        runner.Post(
            new SimulationRequest(
                (poses, _) => Complete(completion, poses, work, cancellationToken)
            )
        );

        return completion.Task.WaitAsync(cancellationToken);
    }

    private static async Task Complete<T>(
        TaskCompletionSource<T> completion,
        LivePlayerPoses poses,
        Func<LivePlayerPoses, CancellationToken, Task<T>> work,
        CancellationToken cancellationToken
    )
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            completion.SetResult(await work(poses, cancellationToken));
        }
        catch (OperationCanceledException)
        {
            completion.SetCanceled(cancellationToken);
        }
        catch (Exception exception)
        {
            completion.SetException(exception);
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
