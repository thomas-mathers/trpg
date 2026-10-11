using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TRPG.Application.Common.Clocks;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Events;
using TRPG.Application.Common.Queries;
using TRPG.Application.GameTurns.Commands;
using TRPG.Application.Scenes.Commands;
using TRPG.Application.WorldSimulation;
using TRPG.Application.WorldSimulation.Commands;
using TRPG.Application.WorldSimulation.Queries;
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
    WorldSimulationCoordinator simulation,
    ILogger<ContinuousWorldProcessor> logger
)
{
    private readonly ConcurrentDictionary<WorldLane, byte> _runningPasses = new();

    public Task ProcessFrequent(CancellationToken cancellationToken = default) =>
        ProcessActiveWorlds(ContinuousWorldLane.Frequent, cancellationToken);

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
        var mutatedWorld = await simulation.Request(
            worldId,
            (_, token) => RunPass(worldId, lane, token),
            cancellationToken
        );
        if (!mutatedWorld)
        {
            return;
        }

        // Flushing can wait on client acks, which must never stall the world's runner.
        await using var scope = serviceScopeFactory.CreateAsyncScope();
        await scope
            .ServiceProvider.GetRequiredService<IGameClientEventDispatcher>()
            .FlushAsync(worldId, cancellationToken);
    }

    private async Task<bool> RunPass(
        Guid worldId,
        ContinuousWorldLane lane,
        CancellationToken cancellationToken
    )
    {
        await using var scope = serviceScopeFactory.CreateAsyncScope();

        var gameTime = await worldClock.GetCurrent(worldId, cancellationToken);
        var players = await scope
            .ServiceProvider.GetRequiredService<
                IQueryHandler<
                    GetActiveLocationPlayersQuery,
                    IReadOnlyCollection<ActiveLocationPlayer>
                >
            >()
            .Handle(new GetActiveLocationPlayersQuery { WorldId = worldId }, cancellationToken);
        if (players.Count == 0)
        {
            return false;
        }

        if (lane == ContinuousWorldLane.Frequent)
        {
            await TickEffects(worldId, players, gameTime, cancellationToken);
            await RegenerateCreatures(worldId, players, gameTime, cancellationToken);
        }
        else
        {
            await SyncRoutines(
                scope.ServiceProvider,
                worldId,
                players,
                gameTime,
                cancellationToken
            );
        }

        await PublishAmbientScenes(worldId, players, gameTime, cancellationToken);
        return true;
    }

    private static Task SyncRoutines(
        IServiceProvider services,
        Guid worldId,
        IReadOnlyCollection<ActiveLocationPlayer> players,
        GameInstant gameTime,
        CancellationToken cancellationToken
    ) =>
        services
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
    }

    private async Task TickEffects(
        Guid worldId,
        IReadOnlyCollection<ActiveLocationPlayer> players,
        GameInstant gameTime,
        CancellationToken cancellationToken
    )
    {
        await using var scope = serviceScopeFactory.CreateAsyncScope();
        var services = scope.ServiceProvider;

        await services
            .GetRequiredService<ICommandHandler<SyncActiveLocationEffectsCommand>>()
            .Handle(
                new SyncActiveLocationEffectsCommand
                {
                    WorldId = worldId,
                    Players = players,
                    GameTime = gameTime,
                },
                cancellationToken
            );
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
    }

    private readonly record struct WorldLane(Guid WorldId, ContinuousWorldLane Lane);
}
