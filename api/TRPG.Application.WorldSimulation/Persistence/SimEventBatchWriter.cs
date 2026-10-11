using System.Transactions;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Queries;
using TRPG.Application.Creatures.Commands;
using TRPG.Application.Routing.Commands;
using TRPG.Application.Weather.Commands;
using TRPG.Application.Worlds.Commands;
using TRPG.Application.Worlds.Queries;
using TRPG.Application.WorldSimulation.Movement;
using TRPG.Application.WorldSimulation.Poses;

namespace TRPG.Application.WorldSimulation.Persistence;

internal sealed class SimEventBatchWriter(Guid worldId, IServiceScopeFactory serviceScopeFactory)
{
    private readonly CreaturePoseMapper _poseMapper = new();

    public async Task Write(IReadOnlyList<SimEvent> events, CancellationToken cancellationToken)
    {
        await using var scope = serviceScopeFactory.CreateAsyncScope();
        using var transaction = new TransactionScope(
            TransactionScopeOption.Required,
            new TransactionOptions { IsolationLevel = IsolationLevel.ReadCommitted },
            TransactionScopeAsyncFlowOption.Enabled
        );
        await WriteCheckpoints(scope.ServiceProvider, events, cancellationToken);
        await WriteStarts(scope.ServiceProvider, events, cancellationToken);
        await WritePoses(scope.ServiceProvider, events, cancellationToken);
        await WriteEffects(scope.ServiceProvider, events, cancellationToken);
        await WriteVitals(scope.ServiceProvider, events, cancellationToken);
        await WriteWeather(scope.ServiceProvider, events, cancellationToken);
        await scope
            .ServiceProvider.GetRequiredService<
                ICommandHandler<StampWorldStateCommand, WorldStateStamp>
            >()
            .Handle(new StampWorldStateCommand { WorldId = worldId }, cancellationToken);
        transaction.Complete();
    }

    public async Task<long> ReadMarker(CancellationToken cancellationToken)
    {
        await using var scope = serviceScopeFactory.CreateAsyncScope();

        return await scope
            .ServiceProvider.GetRequiredService<IQueryHandler<GetWorldStateVersionQuery, long>>()
            .Handle(new GetWorldStateVersionQuery { WorldId = worldId }, cancellationToken);
    }

    private static async Task WriteCheckpoints(
        IServiceProvider services,
        IReadOnlyList<SimEvent> events,
        CancellationToken cancellationToken
    )
    {
        var checkpoints = events.OfType<JourneyCheckpoint>().ToArray();
        if (checkpoints.Length == 0)
        {
            return;
        }

        await services
            .GetRequiredService<ICommandHandler<CheckpointJourneysCommand>>()
            .Handle(
                new CheckpointJourneysCommand
                {
                    Updates = checkpoints
                        .Select(checkpoint => new JourneyCheckpointUpdate(
                            checkpoint.JourneyId,
                            checkpoint.Status,
                            checkpoint.LegIndex,
                            checkpoint.LegProgressMeters,
                            checkpoint.At,
                            checkpoint.PausedAt
                        ))
                        .ToArray(),
                },
                cancellationToken
            );
    }

    private static async Task WriteStarts(
        IServiceProvider services,
        IReadOnlyList<SimEvent> events,
        CancellationToken cancellationToken
    )
    {
        var startedCreatureIds = events
            .OfType<JourneyStarted>()
            .Select(started => started.CreatureId)
            .ToArray();
        if (startedCreatureIds.Length == 0)
        {
            return;
        }

        await services
            .GetRequiredService<ICommandHandler<StartJourneysCommand>>()
            .Handle(
                new StartJourneysCommand { CreatureIds = startedCreatureIds },
                cancellationToken
            );
    }

    private static async Task WriteEffects(
        IServiceProvider services,
        IReadOnlyList<SimEvent> events,
        CancellationToken cancellationToken
    )
    {
        var updates = events
            .OfType<CreatureEffectsAdvanced>()
            .Select(advanced => advanced.Update)
            .ToArray();
        if (updates.Length == 0)
        {
            return;
        }

        await services
            .GetRequiredService<ICommandHandler<PersistCreatureStatesCommand>>()
            .Handle(new PersistCreatureStatesCommand { Updates = updates }, cancellationToken);
    }

    private static async Task WriteVitals(
        IServiceProvider services,
        IReadOnlyList<SimEvent> events,
        CancellationToken cancellationToken
    )
    {
        var writes = events
            .OfType<CreatureRegenerated>()
            .Select(regenerated => new CreatureVitalsWrite(
                regenerated.Vitals,
                regenerated.LastRegenAt
            ))
            .ToArray();
        if (writes.Length == 0)
        {
            return;
        }

        await services
            .GetRequiredService<ICommandHandler<ApplyCreatureVitalsCommand>>()
            .Handle(new ApplyCreatureVitalsCommand { Writes = writes }, cancellationToken);
    }

    private async Task WriteWeather(
        IServiceProvider services,
        IReadOnlyList<SimEvent> events,
        CancellationToken cancellationToken
    )
    {
        var forecasts = events
            .OfType<WeatherChanged>()
            .Select(changed => changed.Forecast)
            .ToArray();
        if (forecasts.Length == 0)
        {
            return;
        }

        await services
            .GetRequiredService<ICommandHandler<ApplyWeatherForecastsCommand>>()
            .Handle(
                new ApplyWeatherForecastsCommand { WorldId = worldId, Forecasts = forecasts },
                cancellationToken
            );
    }

    private async Task WritePoses(
        IServiceProvider services,
        IReadOnlyList<SimEvent> events,
        CancellationToken cancellationToken
    )
    {
        await services
            .GetRequiredService<ICommandHandler<ApplyCreaturePoseUpdatesCommand>>()
            .Handle(
                new ApplyCreaturePoseUpdatesCommand
                {
                    Updates = _poseMapper.Map(
                        events
                            .OfType<CreatureSimEvent>()
                            .Where(simEvent =>
                                simEvent is not (JourneyCheckpoint or CreatureVitalsEvent)
                            )
                    ),
                },
                cancellationToken
            );
    }
}
