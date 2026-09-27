using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TRPG.Application.Common.Clocks;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Concurrency;
using TRPG.Application.Common.Events;
using TRPG.Application.Common.Queries;
using TRPG.Application.GameTurns.Commands;
using TRPG.Application.LocationSimulation;
using TRPG.Application.LocationSimulation.Commands;
using TRPG.Application.LocationSimulation.Queries;
using TRPG.Domain;

namespace TRPG.Worlds;

internal enum ContinuousWorldLane
{
    Frequent,
    Routines,
}

internal sealed class ContinuousWorldProcessor(
    IServiceScopeFactory serviceScopeFactory,
    IWorldClock worldClock,
    IWorldMutationGate mutationGate,
    ILogger<ContinuousWorldProcessor> logger
)
{
    private readonly ConcurrentDictionary<WorldLane, byte> _runningPasses = new();

    public async Task ProcessFrequent(CancellationToken cancellationToken = default)
    {
        foreach (var worldId in worldClock.GetActiveWorldIds())
        {
            await CheckpointClock(worldId, cancellationToken);
        }

        await ProcessActiveWorlds(ContinuousWorldLane.Frequent, cancellationToken);
    }

    public Task ProcessRoutines(CancellationToken cancellationToken = default) =>
        ProcessActiveWorlds(ContinuousWorldLane.Routines, cancellationToken);

    private async Task ProcessActiveWorlds(
        ContinuousWorldLane lane,
        CancellationToken cancellationToken
    ) =>
        await Task.WhenAll(
            worldClock
                .GetActiveWorldIds()
                .Select(worldId => ProcessWorld(worldId, lane, cancellationToken))
        );

    private async Task CheckpointClock(Guid worldId, CancellationToken cancellationToken)
    {
        try
        {
            await worldClock.Checkpoint(worldId, cancellationToken);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogError(
                exception,
                "Failed to checkpoint the clock of world {WorldId}",
                worldId
            );
        }
    }

    private async Task ProcessWorld(
        Guid worldId,
        ContinuousWorldLane lane,
        CancellationToken cancellationToken
    )
    {
        var pass = new WorldLane(worldId, lane);
        if (!_runningPasses.TryAdd(pass, 0))
        {
            logger.LogDebug(
                "Skipping {Lane} simulation for world {WorldId}: the previous pass is still running",
                lane,
                worldId
            );
            return;
        }

        try
        {
            await SyncWorld(worldId, lane, cancellationToken);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogError(
                exception,
                "Failed {Lane} simulation for world {WorldId}",
                lane,
                worldId
            );
        }
        finally
        {
            _runningPasses.TryRemove(pass, out _);
        }
    }

    private async Task SyncWorld(
        Guid worldId,
        ContinuousWorldLane lane,
        CancellationToken cancellationToken
    )
    {
        await using var lease = await mutationGate.Acquire(worldId, cancellationToken);
        await using var scope = serviceScopeFactory.CreateAsyncScope();
        var services = scope.ServiceProvider;

        var gameTime = await worldClock.GetCurrent(worldId, cancellationToken);
        var players = await services
            .GetRequiredService<
                IQueryHandler<
                    GetActiveLocationPlayersQuery,
                    IReadOnlyCollection<ActiveLocationPlayer>
                >
            >()
            .Handle(new GetActiveLocationPlayersQuery { WorldId = worldId }, cancellationToken);
        if (players.Count == 0)
        {
            return;
        }

        if (lane == ContinuousWorldLane.Frequent)
        {
            await services
                .GetRequiredService<ICommandHandler<SyncActiveLocationTravelersCommand>>()
                .Handle(
                    new SyncActiveLocationTravelersCommand
                    {
                        WorldId = worldId,
                        Players = players,
                        GameTime = gameTime,
                    },
                    cancellationToken
                );
            await RegenerateCreatures(worldId, players, gameTime, cancellationToken);
            await PublishAmbientScenes(worldId, players, gameTime, cancellationToken);
            return;
        }

        await services
            .GetRequiredService<ICommandHandler<SyncActiveLocationRoutinesCommand>>()
            .Handle(
                new SyncActiveLocationRoutinesCommand
                {
                    WorldId = worldId,
                    Players = players,
                    GameTime = gameTime,
                },
                cancellationToken
            );

        // Spawn-driven encounters are queued by the routine sync; clients must see them before the scene.
        await services
            .GetRequiredService<IGameClientEventDispatcher>()
            .FlushAsync(worldId, cancellationToken);
        await PublishAmbientScenes(worldId, players, gameTime, cancellationToken);
    }

    private async Task PublishAmbientScenes(
        Guid worldId,
        IReadOnlyCollection<ActiveLocationPlayer> players,
        GameInstant gameTime,
        CancellationToken cancellationToken
    )
    {
        await using var scope = serviceScopeFactory.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var publishAmbientScene = services.GetRequiredService<
            ICommandHandler<PublishAmbientSceneCommand>
        >();

        foreach (var player in players)
        {
            await publishAmbientScene.Handle(
                new PublishAmbientSceneCommand
                {
                    WorldId = worldId,
                    PlayerId = player.PlayerId,
                    GameTime = gameTime,
                },
                cancellationToken
            );
        }

        // Flushing inside the lease keeps the snapshot ordered with the mutations it describes.
        await services
            .GetRequiredService<IGameClientEventDispatcher>()
            .FlushAsync(worldId, cancellationToken);
    }

    private async Task RegenerateCreatures(
        Guid worldId,
        IReadOnlyCollection<ActiveLocationPlayer> players,
        GameInstant gameTime,
        CancellationToken cancellationToken
    )
    {
        await using var scope = serviceScopeFactory.CreateAsyncScope();
        var services = scope.ServiceProvider;

        await services
            .GetRequiredService<ICommandHandler<SyncActiveLocationRegenerationCommand>>()
            .Handle(
                new SyncActiveLocationRegenerationCommand
                {
                    WorldId = worldId,
                    Players = players,
                    GameTime = gameTime,
                },
                cancellationToken
            );

        // Flushing inside the lease keeps vitals ordered with the mutations they describe.
        await services
            .GetRequiredService<IGameClientEventDispatcher>()
            .FlushAsync(worldId, cancellationToken);
    }

    private readonly record struct WorldLane(Guid WorldId, ContinuousWorldLane Lane);
}
