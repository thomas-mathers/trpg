using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Events;
using TRPG.Application.WorldSimulation.Publishing;

namespace TRPG.Application.WorldSimulation.Subscriptions;

internal sealed class SceneResync(
    Guid worldId,
    SceneSubscriptions subscriptions,
    WorldSimulationOptions options,
    IServiceScopeFactory serviceScopeFactory,
    ILogger logger
)
{
    private Task _inFlight = Task.CompletedTask;

    public async Task Run(CancellationToken cancellationToken)
    {
        if (!_inFlight.IsCompleted)
        {
            return;
        }

        CloseExhausted();

        var pending = subscriptions.TakePending(worldId);
        if (pending.Count == 0)
        {
            return;
        }

        var built = await BuildSnapshots(pending, cancellationToken);
        if (built.Count > 0)
        {
            _inFlight = SendSnapshots(built, cancellationToken);
        }
    }

    private async Task<IReadOnlyCollection<Guid>> BuildSnapshots(
        IReadOnlyCollection<Guid> playerIds,
        CancellationToken cancellationToken
    )
    {
        var built = new List<Guid>();
        await using var scope = serviceScopeFactory.CreateAsyncScope();
        var publishScene = scope.ServiceProvider.GetRequiredService<
            ICommandHandler<PublishPlayerSceneCommand>
        >();
        foreach (var playerId in playerIds)
        {
            try
            {
                await publishScene.Handle(
                    new PublishPlayerSceneCommand { WorldId = worldId, PlayerId = playerId },
                    cancellationToken
                );
                built.Add(playerId);
            }
            catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
            {
                logger.LogError(
                    exception,
                    "Scene resync snapshot for player {PlayerId} failed in world {WorldId}",
                    playerId,
                    worldId
                );
                subscriptions.MarkPlayerForResync(worldId, playerId);
            }
        }

        return built;
    }

    private async Task SendSnapshots(
        IReadOnlyCollection<Guid> playerIds,
        CancellationToken cancellationToken
    )
    {
        var attemptTimeout = options.ResyncWindow / options.ResyncAttempts;
        try
        {
            await using var scope = serviceScopeFactory.CreateAsyncScope();
            await scope
                .ServiceProvider.GetRequiredService<IGameClientEventDispatcher>()
                .FlushAsync(worldId, cancellationToken)
                .WaitAsync(attemptTimeout, cancellationToken);
            foreach (var playerId in playerIds)
            {
                subscriptions.CompleteResync(worldId, playerId);
            }
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogError(exception, "Scene resync send failed in world {WorldId}", worldId);
            foreach (var playerId in playerIds)
            {
                subscriptions.MarkPlayerForResync(worldId, playerId);
            }
        }
    }

    private void CloseExhausted()
    {
        var exhausted = subscriptions.TakeExhausted(worldId);
        if (exhausted.Count == 0)
        {
            return;
        }

        using var scope = serviceScopeFactory.CreateScope();
        var closer = scope.ServiceProvider.GetRequiredService<IPlayerConnectionCloser>();
        foreach (var playerId in exhausted)
        {
            logger.LogWarning(
                "Closing the connection of player {PlayerId} in world {WorldId} after repeated scene resync failures",
                playerId,
                worldId
            );
            closer.Close(playerId);
        }
    }
}
