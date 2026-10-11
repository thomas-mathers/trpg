using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using TRPG.Application.Common.Clocks;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Exceptions;
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

    public async Task Post(
        Guid worldId,
        WorldSimulationMessage message,
        CancellationToken cancellationToken = default
    )
    {
        var runner = await GetRunner(worldId, cancellationToken);
        runner.Post(message);
    }

    // A runner that loads later reads the committed state itself, so only a loaded or loading one needs the message.
    public async Task PostIfLoaded(
        Guid worldId,
        WorldSimulationMessage message,
        CancellationToken cancellationToken = default
    )
    {
        if (!_runners.TryGetValue(worldId, out var loading))
        {
            return;
        }

        try
        {
            var runner = await loading.Value.WaitAsync(cancellationToken);
            runner.Post(message);
        }
        catch (Exception exception)
            when (exception is WorldUnavailableException || loading.Value.IsFaulted)
        {
            // A runner that failed to load or is reloading reads the committed state itself.
        }
    }

    public async Task Request(
        Guid worldId,
        Func<LivePlayerPoses, CancellationToken, Task> work,
        CancellationToken cancellationToken
    ) =>
        await Request(
            worldId,
            async (poses, token) =>
            {
                await work(poses, token);
                return true;
            },
            cancellationToken
        );

    public async Task<T> Request<T>(
        Guid worldId,
        Func<LivePlayerPoses, CancellationToken, Task<T>> work,
        CancellationToken cancellationToken
    )
    {
        var runner = await GetRunner(worldId, cancellationToken);
        var completion = new TaskCompletionSource<T>(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        runner.Post(
            new SimulationRequest(
                (poses, _) => Complete(completion, poses, work, cancellationToken),
                exception => completion.TrySetException(exception)
            )
        );

        return await completion.Task.WaitAsync(cancellationToken);
    }

    private static async Task Complete<T>(
        TaskCompletionSource<T> completion,
        LivePlayerPoses poses,
        Func<LivePlayerPoses, CancellationToken, Task<T>> work,
        CancellationToken cancellationToken
    )
    {
        if (cancellationToken.IsCancellationRequested)
        {
            completion.TrySetCanceled(cancellationToken);
            return;
        }

        try
        {
            completion.TrySetResult(await work(poses, cancellationToken));
        }
        catch (Exception exception)
        {
            completion.TrySetException(exception);
            throw;
        }
    }

    public bool IsUnavailable(Guid worldId) => _unavailableWorlds.ContainsKey(worldId);

    public void MarkUnavailable(Guid worldId) => _unavailableWorlds.TryAdd(worldId, 0);

    public async Task EnsureRunner(Guid worldId, CancellationToken cancellationToken = default)
    {
        try
        {
            await AcquireRunner(worldId, cancellationToken);
        }
        catch (Exception exception)
            when (exception is not (OperationCanceledException or EntityNotFoundException))
        {
            MarkUnavailable(worldId);
            throw;
        }
    }

    public async Task Tick(CancellationToken cancellationToken = default)
    {
        var activeWorldIds = worldClock.GetActiveWorldIds().ToHashSet();
        var runners = LoadedRunners();
        await Task.WhenAll(
            runners.Select(runner =>
                TickWorld(runner, activeWorldIds.Contains(runner.WorldId), cancellationToken)
            )
        );
    }

    private async Task TickWorld(
        WorldSimulationRunner runner,
        bool isActive,
        CancellationToken cancellationToken
    )
    {
        var worldId = runner.WorldId;
        if (!isActive && !runner.HasPendingWork)
        {
            return;
        }

        if (!_tickingWorlds.TryAdd(worldId, 0))
        {
            return;
        }

        try
        {
            using var writeScope = WorldWriteScope.Enter();
            var now = await worldClock.GetCurrent(worldId, cancellationToken);
            if (isActive)
            {
                await runner.Tick(now, cancellationToken);
            }
            else
            {
                await runner.ServiceMailbox(now, cancellationToken);
            }
        }
        catch (EntityNotFoundException exception)
        {
            DropRunner(runner, exception);
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

    private void DropRunner(WorldSimulationRunner runner, Exception reason)
    {
        _runners.TryRemove(runner.WorldId, out _);
        runner.RejectQueuedMessages(reason);
    }

    private List<WorldSimulationRunner> LoadedRunners() =>
        [
            .. _runners
                .Values.Select(loading => loading.Value)
                .Where(loading => loading.IsCompletedSuccessfully)
                .Select(loading => loading.Result),
        ];

    private async Task<WorldSimulationRunner> GetRunner(
        Guid worldId,
        CancellationToken cancellationToken
    )
    {
        if (IsUnavailable(worldId))
        {
            throw new WorldUnavailableException(worldId);
        }

        try
        {
            return await AcquireRunner(worldId, cancellationToken);
        }
        catch (Exception exception)
            when (exception is not (OperationCanceledException or EntityNotFoundException))
        {
            logger.LogError(exception, "Loading the simulation of world {WorldId} failed", worldId);
            throw new WorldUnavailableException(worldId, exception);
        }
    }

    private async Task<WorldSimulationRunner> AcquireRunner(
        Guid worldId,
        CancellationToken cancellationToken
    )
    {
        var loading = _runners.GetOrAdd(
            worldId,
            id => new Lazy<Task<WorldSimulationRunner>>(() => LoadRunner(id))
        );

        try
        {
            return await loading.Value.WaitAsync(cancellationToken);
        }
        catch (Exception) when (loading.Value.IsFaulted)
        {
            _runners.TryRemove(KeyValuePair.Create(worldId, loading));
            throw;
        }
    }

    private async Task<WorldSimulationRunner> LoadRunner(Guid worldId)
    {
        var now = await worldClock.GetCurrent(worldId);
        return await runnerFactory.Create(worldId, now);
    }
}
