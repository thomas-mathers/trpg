using System.Diagnostics;
using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Concurrency;
using TRPG.Application.Common.Events;
using TRPG.Application.Creatures.Commands;
using TRPG.Application.Routing.Commands;
using TRPG.Application.WorldSimulation.Arrivals;
using TRPG.Application.WorldSimulation.Movement;
using TRPG.Application.WorldSimulation.Poses;
using TRPG.Application.WorldSimulation.Publishing;
using TRPG.Domain;

namespace TRPG.Application.WorldSimulation;

public sealed class WorldSimulationRunner(
    Guid worldId,
    LoadedWorldSimulation loaded,
    IServiceScopeFactory serviceScopeFactory,
    IWorldMutationGate mutationGate
)
{
    private static readonly TimeSpan WeatherRefreshInterval = TimeSpan.FromSeconds(30);

    private long _lastWeatherRefresh = Stopwatch.GetTimestamp();
    private readonly Channel<WorldSimulationMessage> _mailbox =
        Channel.CreateUnbounded<WorldSimulationMessage>(
            new UnboundedChannelOptions { SingleReader = true }
        );
    private readonly List<SimEvent> _unwrittenEvents = [];
    private readonly List<JourneyCompleted> _unexecutedArrivals = [];
    private readonly Dictionary<Guid, HashSet<Guid>> _unpublishedCreatureIdsByLocationId = [];

    public Guid WorldId => worldId;

    public void Post(WorldSimulationMessage message) => _mailbox.Writer.TryWrite(message);

    public async Task RefreshWeather(GameInstant now, CancellationToken cancellationToken = default)
    {
        await using var scope = serviceScopeFactory.CreateAsyncScope();
        var exposed = await scope
            .ServiceProvider.GetRequiredService<WeatherExposureProvider>()
            .FindExposedLocationIds([.. loaded.Simulator.IdleLocationIds()], cancellationToken);

        loaded.Simulator.SetExposedLocations(exposed, now);
        _lastWeatherRefresh = Stopwatch.GetTimestamp();
    }

    public async Task Tick(GameInstant now, CancellationToken cancellationToken = default)
    {
        if (Stopwatch.GetElapsedTime(_lastWeatherRefresh) >= WeatherRefreshInterval)
        {
            await RefreshWeather(now, cancellationToken);
        }

        await DrainMailbox(now, cancellationToken);
        await PlanJourneys(now, cancellationToken);
        _unwrittenEvents.AddRange(loaded.Simulator.Step(now));
        if (
            _unwrittenEvents.Count == 0
            && _unexecutedArrivals.Count == 0
            && _unpublishedCreatureIdsByLocationId.Count == 0
        )
        {
            return;
        }

        await using var lease = await mutationGate.Acquire(worldId, cancellationToken);
        await WritePoses(cancellationToken);
        await ExecuteArrivals(now, cancellationToken);
        await PublishScenes(now, cancellationToken);
    }

    private async Task DrainMailbox(GameInstant now, CancellationToken cancellationToken)
    {
        while (_mailbox.Reader.TryRead(out var message))
        {
            await Apply(message, now, cancellationToken);
        }
    }

    private async Task PlanJourneys(GameInstant now, CancellationToken cancellationToken)
    {
        var creatureIds = loaded.Simulator.CreaturesAwaitingJourneyPlanning(now);
        if (creatureIds.Count == 0)
        {
            return;
        }

        await using var scope = serviceScopeFactory.CreateAsyncScope();
        var planJourney = scope.ServiceProvider.GetRequiredService<
            ICommandHandler<PlanRoutineJourneyCommand, Guid?>
        >();
        foreach (var creatureId in creatureIds)
        {
            var journeyId = await planJourney.Handle(
                new PlanRoutineJourneyCommand
                {
                    CreatureId = creatureId,
                    PlannedAt = now,
                    TimeScale = loaded.Simulator.Options.TimeScale,
                    ArrivalStagger = loaded.Simulator.Options.ArrivalStagger,
                },
                cancellationToken
            );
            if (journeyId != null)
            {
                await Track(creatureId, now, cancellationToken);
            }
        }
    }

    private async Task Apply(
        WorldSimulationMessage message,
        GameInstant now,
        CancellationToken cancellationToken
    )
    {
        switch (message)
        {
            case EngageCreature engage:
                _unwrittenEvents.AddRange(loaded.Simulator.Engage(engage.CreatureId, now));
                break;
            case ReleaseCreature release:
                _unwrittenEvents.AddRange(loaded.Simulator.Release(release.CreatureId, now));
                break;
            case SpawnCreature spawn:
                loaded.Simulator.Add(spawn.Seed, now);
                break;
            case RemoveCreature remove:
                loaded.Simulator.Remove(remove.CreatureId);
                break;
            case TrackCreature track:
                await Track(track.CreatureId, now, cancellationToken);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(message));
        }
    }

    private async Task Track(Guid creatureId, GameInstant now, CancellationToken cancellationToken)
    {
        await using var scope = serviceScopeFactory.CreateAsyncScope();
        var seed = await scope
            .ServiceProvider.GetRequiredService<WorldSimulatorLoader>()
            .LoadSeed(worldId, creatureId, loaded.Graph, cancellationToken);

        loaded.Simulator.Remove(creatureId);
        if (seed != null)
        {
            loaded.Simulator.Add(seed, now);
        }
    }

    private async Task WritePoses(CancellationToken cancellationToken)
    {
        if (_unwrittenEvents.Count == 0)
        {
            return;
        }

        await using var scope = serviceScopeFactory.CreateAsyncScope();
        var checkpoints = _unwrittenEvents.OfType<JourneyCheckpoint>().ToArray();
        if (checkpoints.Length > 0)
        {
            await scope
                .ServiceProvider.GetRequiredService<ICommandHandler<CheckpointJourneysCommand>>()
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
        var startedCreatureIds = _unwrittenEvents
            .OfType<JourneyStarted>()
            .Select(started => started.CreatureId)
            .ToArray();
        if (startedCreatureIds.Length > 0)
        {
            await scope
                .ServiceProvider.GetRequiredService<ICommandHandler<StartJourneysCommand>>()
                .Handle(
                    new StartJourneysCommand { CreatureIds = startedCreatureIds },
                    cancellationToken
                );
        }
        await scope
            .ServiceProvider.GetRequiredService<ICommandHandler<ApplyCreaturePoseUpdatesCommand>>()
            .Handle(
                new ApplyCreaturePoseUpdatesCommand
                {
                    Updates = loaded.PoseMapper.Map(
                        _unwrittenEvents
                            .OfType<SimEvent>()
                            .Where(simEvent => simEvent is not JourneyCheckpoint)
                    ),
                },
                cancellationToken
            );

        _unexecutedArrivals.AddRange(_unwrittenEvents.OfType<JourneyCompleted>());
        RememberUnpublishedCreatures();
        _unwrittenEvents.Clear();
    }

    private async Task ExecuteArrivals(GameInstant now, CancellationToken cancellationToken)
    {
        if (_unexecutedArrivals.Count == 0)
        {
            return;
        }

        await using var scope = serviceScopeFactory.CreateAsyncScope();
        var result = await scope
            .ServiceProvider.GetRequiredService<
                ICommandHandler<ExecuteJourneyArrivalsCommand, JourneyArrivalResult>
            >()
            .Handle(
                new ExecuteJourneyArrivalsCommand
                {
                    WorldId = worldId,
                    Arrivals = [.. _unexecutedArrivals],
                },
                cancellationToken
            );

        foreach (var creatureId in result.ReroutedCreatureIds)
        {
            await Track(creatureId, now, cancellationToken);
        }

        _unexecutedArrivals.Clear();
    }

    private async Task PublishScenes(GameInstant now, CancellationToken cancellationToken)
    {
        if (_unpublishedCreatureIdsByLocationId.Count == 0)
        {
            return;
        }

        await using var scope = serviceScopeFactory.CreateAsyncScope();
        await scope
            .ServiceProvider.GetRequiredService<ICommandHandler<PublishSimulationScenesCommand>>()
            .Handle(
                new PublishSimulationScenesCommand
                {
                    WorldId = worldId,
                    GameTime = now,
                    ChangedCreatureIdsByLocationId =
                        _unpublishedCreatureIdsByLocationId.ToDictionary(
                            entry => entry.Key,
                            entry => (IReadOnlySet<Guid>)entry.Value
                        ),
                },
                cancellationToken
            );

        // Flushing inside the lease keeps the scene ordered with the mutations it describes.
        await scope
            .ServiceProvider.GetRequiredService<IGameClientEventDispatcher>()
            .FlushAsync(worldId, cancellationToken);
        _unpublishedCreatureIdsByLocationId.Clear();
    }

    private void RememberUnpublishedCreatures()
    {
        foreach (var simEvent in _unwrittenEvents)
        {
            foreach (var locationId in SimEventLocations.Touched(simEvent))
            {
                if (
                    !_unpublishedCreatureIdsByLocationId.TryGetValue(
                        locationId,
                        out var creatureIds
                    )
                )
                {
                    creatureIds = [];
                    _unpublishedCreatureIdsByLocationId[locationId] = creatureIds;
                }

                creatureIds.Add(simEvent.CreatureId);
            }
        }
    }
}
