using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Events;
using TRPG.Application.Common.Queries;
using TRPG.Application.WorldSimulation.Commands;
using TRPG.Application.WorldSimulation.Queries;
using TRPG.Domain;

namespace TRPG.Application.WorldSimulation;

internal sealed class ActiveLocationPasses(
    Guid worldId,
    TimeSpan interval,
    IServiceScopeFactory serviceScopeFactory,
    ILogger logger
)
{
    private readonly DueEntry _effects = new(interval);
    private readonly DueEntry _regeneration = new(interval);

    public async Task Run(GameInstant now, CancellationToken cancellationToken)
    {
        var effectsDue = _effects.TakeIfDue(now);
        var regenerationDue = _regeneration.TakeIfDue(now);
        if (!effectsDue && !regenerationDue)
        {
            return;
        }

        try
        {
            await RunDue(effectsDue, regenerationDue, now, cancellationToken);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogError(
                exception,
                "Active location passes failed in world {WorldId}; retrying when next due",
                worldId
            );
        }
    }

    private async Task RunDue(
        bool effectsDue,
        bool regenerationDue,
        GameInstant now,
        CancellationToken cancellationToken
    )
    {
        await using var scope = serviceScopeFactory.CreateAsyncScope();
        var services = scope.ServiceProvider;

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

        if (effectsDue)
        {
            await Isolate(
                "effects",
                () =>
                    services
                        .GetRequiredService<ICommandHandler<SyncActiveLocationEffectsCommand>>()
                        .Handle(
                            new SyncActiveLocationEffectsCommand
                            {
                                WorldId = worldId,
                                Players = players,
                                GameTime = now,
                            },
                            cancellationToken
                        ),
                cancellationToken
            );
        }

        if (regenerationDue)
        {
            await Isolate(
                "regeneration",
                () =>
                    services
                        .GetRequiredService<
                            ICommandHandler<SyncActiveLocationRegenerationCommand>
                        >()
                        .Handle(
                            new SyncActiveLocationRegenerationCommand
                            {
                                WorldId = worldId,
                                Players = players,
                                GameTime = now,
                            },
                            cancellationToken
                        ),
                cancellationToken
            );
        }

        await services
            .GetRequiredService<IGameClientEventDispatcher>()
            .FlushAsync(worldId, cancellationToken);
    }

    private async Task Isolate(string pass, Func<Task> work, CancellationToken cancellationToken)
    {
        try
        {
            await work();
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogError(
                exception,
                "Active location {Pass} pass failed in world {WorldId}; retrying when next due",
                pass,
                worldId
            );
        }
    }
}
