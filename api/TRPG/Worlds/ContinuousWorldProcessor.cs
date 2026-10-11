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
using TRPG.Application.WorldSimulation.Queries;
using TRPG.Domain;

namespace TRPG.Worlds;

internal sealed class ContinuousWorldProcessor(
    IServiceScopeFactory serviceScopeFactory,
    IWorldClock worldClock,
    WorldSimulationCoordinator simulation,
    ILogger<ContinuousWorldProcessor> logger
)
{
    private readonly ConcurrentDictionary<Guid, byte> _runningPasses = new();

    public async Task Process(CancellationToken cancellationToken = default) =>
        await Task.WhenAll(
            worldClock
                .GetActiveWorldIds()
                .Select(worldId => ProcessWorld(worldId, cancellationToken))
        );

    private async Task ProcessWorld(Guid worldId, CancellationToken cancellationToken)
    {
        if (!_runningPasses.TryAdd(worldId, 0))
        {
            logger.LogDebug(
                "Skipping ambient publish for world {WorldId}: the previous pass is still running",
                worldId
            );
            return;
        }

        try
        {
            await SyncWorld(worldId, cancellationToken);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogError(exception, "Failed ambient publish for world {WorldId}", worldId);
        }
        finally
        {
            _runningPasses.TryRemove(worldId, out _);
        }
    }

    private async Task SyncWorld(Guid worldId, CancellationToken cancellationToken)
    {
        var mutatedWorld = await simulation.Request(
            worldId,
            (_, token) => RunPass(worldId, token),
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

    private async Task<bool> RunPass(Guid worldId, CancellationToken cancellationToken)
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

        await PublishAmbientScenes(worldId, players, gameTime, cancellationToken);
        return true;
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
    }
}
