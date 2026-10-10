using System.Diagnostics;
using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Concurrency;
using TRPG.Application.Common.Events;
using TRPG.Application.Creatures.Commands;
using TRPG.Application.Routing.Commands;
using TRPG.Application.Scenes.Queries;
using TRPG.Application.WorldSimulation.Arrivals;
using TRPG.Application.WorldSimulation.LocalActivities;
using TRPG.Application.WorldSimulation.Movement;
using TRPG.Application.WorldSimulation.Poses;
using TRPG.Application.WorldSimulation.Publishing;
using TRPG.Domain;
using TRPG.Domain.Models;

namespace TRPG.Application.WorldSimulation;

public sealed class WorldSimulationRunner(
    Guid worldId,
    LoadedWorldSimulation loaded,
    IServiceScopeFactory serviceScopeFactory,
    IWorldMutationGate mutationGate,
    TransientCreatureWalkRegistry transientWalks,
    ILogger<WorldSimulationRunner> logger
)
{
    private static readonly TimeSpan WeatherRefreshInterval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan SlowTickThreshold = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan LateMovementThreshold = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan NoteworthyPlanningSweepThreshold = TimeSpan.FromSeconds(1);
    private const int NoteworthyPlanningSweepCreatureCount = 32;

    private long _lastWeatherRefresh = Stopwatch.GetTimestamp();
    private readonly Channel<WorldSimulationMessage> _mailbox =
        Channel.CreateUnbounded<WorldSimulationMessage>(
            new UnboundedChannelOptions { SingleReader = true }
        );
    private readonly List<SimEvent> _unwrittenEvents = [];
    private readonly List<JourneyCompleted> _unexecutedArrivals = [];
    private readonly List<LocalMoveCompleted> _unexecutedLocalArrivals = [];
    private readonly Dictionary<Guid, HashSet<Guid>> _unpublishedCreatureIdsByLocationId = [];
    private Guid? _lastPlannedCreatureId;
    private PlanningSweep? _planningSweep;

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
        var tickStartedAt = Stopwatch.GetTimestamp();
        if (Stopwatch.GetElapsedTime(_lastWeatherRefresh) >= WeatherRefreshInterval)
        {
            await RefreshWeather(now, cancellationToken);
        }

        await DrainMailbox(now, cancellationToken);
        var planningStartedAt = Stopwatch.GetTimestamp();
        var planning = await PlanMovements(now, cancellationToken);
        var planningElapsed = Stopwatch.GetElapsedTime(planningStartedAt);
        _unwrittenEvents.AddRange(loaded.Simulator.Step(now));
        var eventCount = _unwrittenEvents.Count;
        if (
            _unwrittenEvents.Count == 0
            && _unexecutedArrivals.Count == 0
            && _unexecutedLocalArrivals.Count == 0
            && _unpublishedCreatureIdsByLocationId.Count == 0
        )
        {
            LogTick(now, planning, planningElapsed, eventCount, tickStartedAt);
            return;
        }

        await using var lease = await mutationGate.Acquire(worldId, cancellationToken);
        await WritePoses(now, cancellationToken);
        await ExecuteArrivals(cancellationToken);
        await WritePoses(now, cancellationToken);
        await PublishScenes(now, cancellationToken);
        LogTick(now, planning, planningElapsed, eventCount, tickStartedAt);
    }

    private async Task DrainMailbox(GameInstant now, CancellationToken cancellationToken)
    {
        while (_mailbox.Reader.TryRead(out var message))
        {
            await Apply(message, now, cancellationToken);
        }
    }

    private async Task<MovementPlanningSummary> PlanMovements(
        GameInstant now,
        CancellationToken cancellationToken
    )
    {
        var awaitingCreatureIds = loaded.Simulator.CreaturesAwaitingJourneyPlanning(now);
        RefreshPlanningSweep(awaitingCreatureIds, now);
        if (awaitingCreatureIds.Count == 0)
        {
            return new MovementPlanningSummary(0, 0, 0);
        }

        _planningSweep ??= PlanningSweep.Start(awaitingCreatureIds, now);
        var creatureIds = SelectPlanningCandidates(awaitingCreatureIds);

        await using var scope = serviceScopeFactory.CreateAsyncScope();
        var planLocal = scope.ServiceProvider.GetRequiredService<
            ICommandHandler<PlanCurrentLocalActivityCommand, LocalActivityPlanResult>
        >();
        var planJourney = scope.ServiceProvider.GetRequiredService<
            ICommandHandler<PlanRoutineJourneyCommand, Guid?>
        >();
        var localMoves = 0;
        var journeys = 0;
        foreach (var creatureId in creatureIds)
        {
            var local = await planLocal.Handle(
                new PlanCurrentLocalActivityCommand { CreatureId = creatureId, At = now },
                cancellationToken
            );
            if (local.Move is { } localMove)
            {
                _unwrittenEvents.AddRange(loaded.Simulator.StartLocalMove(localMove, now));
                localMoves++;
                continue;
            }
            if (local.Handled)
            {
                loaded.Simulator.SleepUntilNextRoutineChange(creatureId, now);
                continue;
            }

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
                journeys++;
                continue;
            }

            loaded.Simulator.SleepUntilNextRoutineChange(creatureId, now);
        }

        CompletePlanningSweep(creatureIds, localMoves, journeys, now);
        return new MovementPlanningSummary(awaitingCreatureIds.Count, localMoves, journeys);
    }

    private void RefreshPlanningSweep(
        IReadOnlyCollection<Guid> awaitingCreatureIds,
        GameInstant now
    )
    {
        if (_planningSweep is null)
        {
            return;
        }

        _planningSweep.RemainingCreatureIds.IntersectWith(awaitingCreatureIds);
        LogCompletedPlanningSweep(now);
    }

    private void CompletePlanningSweep(
        IReadOnlyCollection<Guid> creatureIds,
        int localMoves,
        int journeys,
        GameInstant now
    )
    {
        if (_planningSweep is not { } sweep)
        {
            return;
        }

        sweep.RemainingCreatureIds.ExceptWith(creatureIds);
        sweep.LocalMoveCount += localMoves;
        sweep.JourneyCount += journeys;
        LogCompletedPlanningSweep(now);
    }

    private void LogCompletedPlanningSweep(GameInstant now)
    {
        if (_planningSweep is not { RemainingCreatureIds.Count: 0 } sweep)
        {
            return;
        }

        var elapsed = Stopwatch.GetElapsedTime(sweep.StartedAt);
        if (
            elapsed >= NoteworthyPlanningSweepThreshold
            || sweep.InitialCreatureCount >= NoteworthyPlanningSweepCreatureCount
            || sweep.LocalMoveCount > 0
            || sweep.JourneyCount > 0
        )
        {
            logger.LogDebug(
                "Completed planning sweep for {CreatureCount} due creatures in {Elapsed}; game time advanced {GameTimeElapsed}; started {LocalMoveCount} local moves and {JourneyCount} journeys",
                sweep.InitialCreatureCount,
                elapsed,
                now - sweep.StartedAtGameTime,
                sweep.LocalMoveCount,
                sweep.JourneyCount
            );
        }
        else
        {
            logger.LogTrace(
                "Completed planning sweep for {CreatureCount} due creatures in {Elapsed}; game time advanced {GameTimeElapsed}; started {LocalMoveCount} local moves and {JourneyCount} journeys",
                sweep.InitialCreatureCount,
                elapsed,
                now - sweep.StartedAtGameTime,
                sweep.LocalMoveCount,
                sweep.JourneyCount
            );
        }
        _planningSweep = null;
    }

    private IReadOnlyList<Guid> SelectPlanningCandidates(IReadOnlyList<Guid> candidates)
    {
        var lastIndex = _lastPlannedCreatureId is { } last ? IndexOf(candidates, last) : -1;
        var startIndex = lastIndex < 0 ? 0 : (lastIndex + 1) % candidates.Count;
        var count = Math.Min(loaded.Simulator.Options.RouteSearchesPerTick, candidates.Count);
        var selected = Enumerable
            .Range(0, count)
            .Select(offset => candidates[(startIndex + offset) % candidates.Count])
            .ToArray();

        _lastPlannedCreatureId = selected[^1];
        return selected;
    }

    private static int IndexOf(IReadOnlyList<Guid> candidates, Guid creatureId)
    {
        for (var index = 0; index < candidates.Count; index++)
        {
            if (candidates[index] == creatureId)
            {
                return index;
            }
        }

        return -1;
    }

    private void LogMovementEvents(GameInstant now, IEnumerable<SimEvent> events)
    {
        foreach (var simEvent in events.Where(simEvent => simEvent is not JourneyCheckpoint))
        {
            var diagnostic = MovementDiagnostic.From(simEvent);
            var delay = now - simEvent.At;
            if (delay >= LateMovementThreshold)
            {
                logger.LogWarning(
                    "Movement {MovementEvent} for creature {CreatureId} at {EventAt} processed at {TickAt} with delay {GameTimeDelay}; location {LocationId}, target {TargetId}, path points {PathPointCount}, path meters {PathMeters:F2}",
                    simEvent.GetType().Name,
                    simEvent.CreatureId,
                    simEvent.At,
                    now,
                    delay,
                    diagnostic.LocationId,
                    diagnostic.TargetId,
                    diagnostic.PathPointCount,
                    diagnostic.PathMeters
                );
                continue;
            }

            logger.LogTrace(
                "Movement {MovementEvent} for creature {CreatureId} at {EventAt} processed at {TickAt} with delay {GameTimeDelay}; location {LocationId}, target {TargetId}, path points {PathPointCount}, path meters {PathMeters:F2}",
                simEvent.GetType().Name,
                simEvent.CreatureId,
                simEvent.At,
                now,
                delay,
                diagnostic.LocationId,
                diagnostic.TargetId,
                diagnostic.PathPointCount,
                diagnostic.PathMeters
            );
        }
    }

    private void LogTick(
        GameInstant now,
        MovementPlanningSummary planning,
        TimeSpan planningElapsed,
        int eventCount,
        long tickStartedAt
    )
    {
        var elapsed = Stopwatch.GetElapsedTime(tickStartedAt);
        if (elapsed >= SlowTickThreshold)
        {
            logger.LogWarning(
                "World simulation tick for {WorldId} at {GameTime} took {ElapsedMilliseconds:F0} ms; planning took {PlanningMilliseconds:F0} ms for {AwaitingCount} creatures, starting {LocalMoveCount} local moves and {JourneyCount} journeys; emitted {EventCount} events",
                worldId,
                now,
                elapsed.TotalMilliseconds,
                planningElapsed.TotalMilliseconds,
                planning.AwaitingCount,
                planning.LocalMoveCount,
                planning.JourneyCount,
                eventCount
            );
            return;
        }

        logger.LogTrace(
            "World simulation tick for {WorldId} at {GameTime} took {ElapsedMilliseconds:F0} ms; planning took {PlanningMilliseconds:F0} ms for {AwaitingCount} creatures, starting {LocalMoveCount} local moves and {JourneyCount} journeys; emitted {EventCount} events",
            worldId,
            now,
            elapsed.TotalMilliseconds,
            planningElapsed.TotalMilliseconds,
            planning.AwaitingCount,
            planning.LocalMoveCount,
            planning.JourneyCount,
            eventCount
        );
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
                transientWalks.Remove(remove.CreatureId);
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

    private async Task WritePoses(GameInstant now, CancellationToken cancellationToken)
    {
        if (_unwrittenEvents.Count == 0)
        {
            return;
        }

        LogMovementEvents(now, _unwrittenEvents);
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
        _unexecutedLocalArrivals.AddRange(_unwrittenEvents.OfType<LocalMoveCompleted>());
        UpdateTransientWalks();
        RememberUnpublishedCreatures();
        _unwrittenEvents.Clear();
    }

    private async Task ExecuteArrivals(CancellationToken cancellationToken)
    {
        if (_unexecutedArrivals.Count == 0 && _unexecutedLocalArrivals.Count == 0)
        {
            return;
        }

        await using var scope = serviceScopeFactory.CreateAsyncScope();
        await ExecuteJourneyArrivals(scope.ServiceProvider, cancellationToken);
        await ExecuteLocalArrivals(scope.ServiceProvider, cancellationToken);
        _unexecutedArrivals.Clear();
        _unexecutedLocalArrivals.Clear();
    }

    private async Task ExecuteJourneyArrivals(
        IServiceProvider services,
        CancellationToken cancellationToken
    )
    {
        if (_unexecutedArrivals.Count == 0)
        {
            return;
        }

        var result = await services
            .GetRequiredService<
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
        foreach (var move in result.LocalMoves)
        {
            var arrivalAt = _unexecutedArrivals
                .First(arrival => arrival.CreatureId == move.CreatureId)
                .At;
            _unwrittenEvents.AddRange(loaded.Simulator.StartLocalMove(move, arrivalAt));
        }
    }

    private async Task ExecuteLocalArrivals(
        IServiceProvider services,
        CancellationToken cancellationToken
    )
    {
        var completeLocal = services.GetRequiredService<
            ICommandHandler<CompleteLocalMoveCommand, LocalMovePlan?>
        >();
        foreach (var arrival in _unexecutedLocalArrivals)
        {
            var retry = await completeLocal.Handle(
                new CompleteLocalMoveCommand
                {
                    Move = arrival.Move,
                    StopPosition = arrival.StopPosition,
                },
                cancellationToken
            );
            if (retry is not null)
            {
                _unwrittenEvents.AddRange(loaded.Simulator.StartLocalMove(retry, arrival.At));
            }
        }
    }

    private void UpdateTransientWalks()
    {
        foreach (var simEvent in _unwrittenEvents)
        {
            switch (simEvent)
            {
                case LocalMoveStarted started:
                    transientWalks.Set(
                        started.CreatureId,
                        started.LocationId,
                        started.Move.Path,
                        started.At,
                        started.MetersPerGameSecond
                    );
                    break;
                case LocalMoveCompleted completed:
                    transientWalks.Remove(completed.CreatureId);
                    break;
                case LocalMoveInterrupted interrupted:
                    transientWalks.Remove(interrupted.CreatureId);
                    break;
                case JourneyStarted started:
                    transientWalks.Remove(started.CreatureId);
                    break;
            }
        }
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

    private sealed record MovementPlanningSummary(
        int AwaitingCount,
        int LocalMoveCount,
        int JourneyCount
    );

    private sealed class PlanningSweep
    {
        private PlanningSweep(IReadOnlyCollection<Guid> creatureIds, GameInstant now)
        {
            RemainingCreatureIds = [.. creatureIds];
            InitialCreatureCount = creatureIds.Count;
            StartedAt = Stopwatch.GetTimestamp();
            StartedAtGameTime = now;
        }

        public HashSet<Guid> RemainingCreatureIds { get; }
        public int InitialCreatureCount { get; }
        public long StartedAt { get; }
        public GameInstant StartedAtGameTime { get; }
        public int LocalMoveCount { get; set; }
        public int JourneyCount { get; set; }

        public static PlanningSweep Start(IReadOnlyCollection<Guid> creatureIds, GameInstant now) =>
            new(creatureIds, now);
    }

    private sealed record MovementDiagnostic(
        Guid? LocationId,
        Guid? TargetId,
        int PathPointCount,
        double PathMeters
    )
    {
        public static MovementDiagnostic From(SimEvent simEvent) =>
            simEvent switch
            {
                JourneyStarted started => new(
                    started.OriginLocationId,
                    started.DestinationLocationId,
                    0,
                    0
                ),
                LocationEntered entered => new(entered.ToLocationId, entered.NextConnectorId, 0, 0),
                JourneyLegCompleted completed => new(
                    completed.LocationId,
                    completed.ArrivalNodeId,
                    0,
                    0
                ),
                JourneyCompleted completed => new(completed.LocationId, completed.JobId, 0, 0),
                LocalMoveStarted started => From(started.LocationId, started.Move),
                LocalMoveCompleted completed => From(completed.LocationId, completed.Move),
                LocalMoveInterrupted interrupted => new(interrupted.LocationId, null, 0, 0),
                _ => new(null, null, 0, 0),
            };

        private static MovementDiagnostic From(Guid locationId, LocalMovePlan move) =>
            new(locationId, move.TargetPropId, move.Path.Count, PathLength(move.Path));

        private static double PathLength(IReadOnlyList<Point> points) =>
            points
                .Zip(points.Skip(1))
                .Sum(pair =>
                    Math.Sqrt(
                        Math.Pow(pair.Second.X - pair.First.X, 2)
                            + Math.Pow(pair.Second.Y - pair.First.Y, 2)
                    )
                );
    }
}
