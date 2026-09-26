using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TRPG.Application.Common.Clocks;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Concurrency;
using TRPG.Application.Common.Queries;
using TRPG.Application.LocationSimulation;
using TRPG.Application.LocationSimulation.Commands;
using TRPG.Application.LocationSimulation.Queries;

namespace TRPG.Worlds;

internal enum ContinuousWorldLane
{
    Travelers,
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

    public async Task ProcessTravelers(CancellationToken cancellationToken = default)
    {
        foreach (var worldId in worldClock.GetActiveWorldIds())
        {
            await CheckpointClock(worldId, cancellationToken);
        }

        await ProcessActiveWorlds(ContinuousWorldLane.Travelers, cancellationToken);
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

        if (lane == ContinuousWorldLane.Travelers)
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
    }

    private readonly record struct WorldLane(Guid WorldId, ContinuousWorldLane Lane);
}
