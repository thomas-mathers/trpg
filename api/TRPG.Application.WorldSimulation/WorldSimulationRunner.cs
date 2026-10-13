using System.Diagnostics;
using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TRPG.Application.Common.Clocks;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Events;
using TRPG.Application.Common.Navigation;
using TRPG.Application.Common.Queries;
using TRPG.Application.Creatures;
using TRPG.Application.Creatures.Commands;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.Routing.Commands;
using TRPG.Application.Scenes.Queries;
using TRPG.Application.WorldSimulation.Arrivals;
using TRPG.Application.WorldSimulation.Climate;
using TRPG.Application.WorldSimulation.Commands;
using TRPG.Application.WorldSimulation.LocalActivities;
using TRPG.Application.WorldSimulation.Movement;
using TRPG.Application.WorldSimulation.Persistence;
using TRPG.Application.WorldSimulation.Poses;
using TRPG.Application.WorldSimulation.Publishing;
using TRPG.Application.WorldSimulation.Subscriptions;
using TRPG.Application.WorldSimulation.Vitals;
using TRPG.Domain;
using TRPG.Domain.Models;

namespace TRPG.Application.WorldSimulation;

public sealed class WorldSimulationRunner(
    Guid worldId,
    LoadedWorldSimulation loaded,
    WorldSimulationOptions options,
    SimulationCadence cadence,
    IWorldClock worldClock,
    IServiceScopeFactory serviceScopeFactory,
    TransientCreatureWalkRegistry transientWalks,
    SceneSubscriptions subscriptions,
    ILogger<WorldSimulationRunner> logger
)
{
    private static readonly TimeSpan SlowTickThreshold = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan LateMovementThreshold = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan NoteworthyPlanningSweepThreshold = TimeSpan.FromSeconds(1);
    private const int NoteworthyPlanningSweepCreatureCount = 32;

    private readonly DueEntry _weatherRefresh = new(cadence.WeatherRefresh);
    private readonly Channel<WorldSimulationMessage> _mailbox =
        Channel.CreateUnbounded<WorldSimulationMessage>(
            new UnboundedChannelOptions { SingleReader = true }
        );
    private readonly LivePlayerPoses _playerPoses = new();
    private readonly MovementSpaceCache _movementSpaces = new();
    private readonly SimEventApplier _applier = SimEventApplier.ForWorld(
        worldId,
        serviceScopeFactory,
        options.MaximumConsecutiveBatchFailures
    );
    private readonly List<JourneyCompleted> _unexecutedArrivals = [];
    private readonly List<LocalMoveCompleted> _unexecutedLocalArrivals = [];
    private readonly List<CreatureVitalsEvent> _unpublishedVitals = [];
    private readonly Dictionary<Guid, HashSet<Guid>> _unpublishedCreatureIdsByLocationId = [];
    private readonly ActiveLocationPasses _activeLocationPasses = new(
        worldId,
        cadence,
        serviceScopeFactory,
        logger
    );
    private readonly VitalsPass _vitals = new(worldId, cadence, serviceScopeFactory, logger);
    private readonly WeatherPass _weather = new(worldId, serviceScopeFactory, logger);
    private readonly SceneResync _sceneResync = new(
        worldId,
        subscriptions,
        options,
        serviceScopeFactory,
        logger
    );
    private LoadedWorldSimulation _loaded = loaded;
    private volatile bool _isRecovering;
    private bool _exposureStale;
    private bool _weatherUnannounced;
    private Guid? _lastPlannedCreatureId;
    private PlanningSweep? _planningSweep;

    public Guid WorldId => worldId;

    public bool HasPendingWork =>
        _isRecovering || _applier.IsBlocked || _mailbox.Reader.TryPeek(out _);

    public void Post(WorldSimulationMessage message)
    {
        if (_isRecovering)
        {
            throw new WorldUnavailableException(worldId);
        }

        _mailbox.Writer.TryWrite(message);
    }

    public async Task RefreshWeather(GameInstant now, CancellationToken cancellationToken = default)
    {
        _applier.Enqueue(await _weather.Run(now, cancellationToken));
        await RefreshExposure(now, cancellationToken);
        _weatherRefresh.Restart(now);
    }

    private async Task RefreshExposure(GameInstant now, CancellationToken cancellationToken)
    {
        await using var scope = serviceScopeFactory.CreateAsyncScope();
        var severe = await scope
            .ServiceProvider.GetRequiredService<WeatherExposureProvider>()
            .FindSevereWeatherLocationIds(
                [.. _loaded.Simulator.IdleLocationIds()],
                cancellationToken
            );

        _loaded.Simulator.SetSevereWeatherLocations(severe, now);
        _exposureStale = false;
    }

    public async Task ServiceMailbox(GameInstant now, CancellationToken cancellationToken = default)
    {
        if (await EnsureWritable(now, cancellationToken))
        {
            await DrainMailbox(now, cancellationToken);
        }
    }

    public async Task Tick(GameInstant now, CancellationToken cancellationToken = default)
    {
        var tickStartedAt = Stopwatch.GetTimestamp();
        if (!await EnsureWritable(now, cancellationToken))
        {
            return;
        }

        now = await DrainMailbox(now, cancellationToken);
        if (_isRecovering)
        {
            return;
        }

        if (_weatherRefresh.TakeIfDue(now))
        {
            await RefreshWeather(now, cancellationToken);
        }
        else if (_exposureStale)
        {
            await RefreshExposure(now, cancellationToken);
        }

        await _sceneResync.Run(cancellationToken);
        await _activeLocationPasses.Run(now, cancellationToken);
        await FlushPlayerPoses(cancellationToken);
        var planningStartedAt = Stopwatch.GetTimestamp();
        var planning = await PlanMovements(now, cancellationToken);
        var planningElapsed = Stopwatch.GetElapsedTime(planningStartedAt);
        _applier.Enqueue(await _vitals.Run(_loaded.Simulator, now, cancellationToken));
        _applier.Enqueue(_loaded.Simulator.Step(now));
        var eventCount = _applier.PendingCount;
        if (
            !_applier.HasPending
            && _unexecutedArrivals.Count == 0
            && _unexecutedLocalArrivals.Count == 0
            && _unpublishedVitals.Count == 0
            && !_weatherUnannounced
            && _unpublishedCreatureIdsByLocationId.Count == 0
        )
        {
            LogTick(now, planning, planningElapsed, eventCount, tickStartedAt);
            return;
        }

        if (!await TryCommit(now, cancellationToken))
        {
            return;
        }

        await ExecuteArrivals(cancellationToken);
        if (!await TryCommit(now, cancellationToken))
        {
            return;
        }

        await PublishScenes(now, cancellationToken);
        LogTick(now, planning, planningElapsed, eventCount, tickStartedAt);
    }

    private async Task<GameInstant> DrainMailbox(
        GameInstant now,
        CancellationToken cancellationToken
    )
    {
        var drainStartedAt = Stopwatch.GetTimestamp();
        var handled = 0;
        while (
            handled < options.MessagesPerTick
            && (
                handled == 0 || Stopwatch.GetElapsedTime(drainStartedAt) < options.MessageTimeBudget
            )
            && _mailbox.Reader.TryRead(out var message)
        )
        {
            handled++;
            now = await ApplyOrRecover(message, now, cancellationToken);
            if (_isRecovering)
            {
                break;
            }
        }

        return now;
    }

    private async Task<GameInstant> ApplyOrRecover(
        WorldSimulationMessage message,
        GameInstant now,
        CancellationToken cancellationToken
    )
    {
        try
        {
            await Apply(message, now, cancellationToken);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogError(
                exception,
                "Simulation message {MessageType} failed in world {WorldId}; reloading from the database",
                message.GetType().Name,
                worldId
            );
            await BeginRecovery(now, cancellationToken);

            return now;
        }

        return message is SimulationRequest
            ? await worldClock.GetCurrent(worldId, cancellationToken)
            : now;
    }

    private async Task<bool> EnsureHealthy(GameInstant now, CancellationToken cancellationToken)
    {
        if (!_isRecovering)
        {
            return true;
        }

        try
        {
            await Reload(now, cancellationToken);
            _isRecovering = false;

            return true;
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogError(
                exception,
                "World {WorldId} failed to reload its simulation and stays unavailable until it can",
                worldId
            );
            RejectQueuedMessages(new WorldUnavailableException(worldId));

            return false;
        }
    }

    private async Task Reload(GameInstant now, CancellationToken cancellationToken)
    {
        var committed = await _applier.ResolveUncertain(cancellationToken);
        OnCommitted(committed, now);

        await using var scope = serviceScopeFactory.CreateAsyncScope();
        _loaded = await scope
            .ServiceProvider.GetRequiredService<WorldSimulatorLoader>()
            .Load(worldId, now, cancellationToken);
        _planningSweep = null;
        _lastPlannedCreatureId = null;

        await RefreshWeather(now, cancellationToken);
    }

    public void RejectQueuedMessages(Exception reason)
    {
        while (_mailbox.Reader.TryRead(out var message))
        {
            if (message is SimulationRequest request)
            {
                request.Reject(reason);
            }
        }
    }

    private async Task FlushPlayerPoses(CancellationToken cancellationToken)
    {
        if (!_playerPoses.HasUnsavedPoses)
        {
            return;
        }

        await using var scope = serviceScopeFactory.CreateAsyncScope();
        var savePlayerPose = scope.ServiceProvider.GetRequiredService<
            ICommandHandler<SavePlayerPoseCommand>
        >();
        foreach (var (playerId, pose) in _playerPoses.Unsaved())
        {
            await savePlayerPose.Handle(
                new SavePlayerPoseCommand
                {
                    PlayerId = playerId,
                    LocationId = pose.LocationId,
                    X = pose.Position.X,
                    Y = pose.Position.Y,
                    Angle = pose.Input.Heading,
                },
                cancellationToken
            );
        }

        _playerPoses.MarkAllSaved();
    }

    private async Task<MovementPlanningSummary> PlanMovements(
        GameInstant now,
        CancellationToken cancellationToken
    )
    {
        var awaitingCreatureIds = _loaded.Simulator.CreaturesAwaitingRoutinePlanning(now);
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
                new PlanCurrentLocalActivityCommand
                {
                    CreatureId = creatureId,
                    At = now,
                    ShelterLocationId = _loaded.Simulator.ShelterLocationOf(creatureId),
                    SevereWeatherLocationIds = _loaded.Simulator.SevereWeatherLocationIds,
                },
                cancellationToken
            );
            if (local.Move is { } localMove)
            {
                _applier.Enqueue(_loaded.Simulator.StartLocalMove(localMove, now));
                localMoves++;
                continue;
            }
            if (local.Handled)
            {
                _loaded.Simulator.DeferUntilNextRoutineChange(creatureId, now);
                continue;
            }

            var journeyId = await planJourney.Handle(
                new PlanRoutineJourneyCommand
                {
                    CreatureId = creatureId,
                    PlannedAt = now,
                    TimeScale = _loaded.Simulator.Options.TimeScale,
                    ArrivalStagger = _loaded.Simulator.Options.ArrivalStagger,
                    ShelterLocationId = _loaded.Simulator.ShelterLocationOf(creatureId),
                    SevereWeatherLocationIds = _loaded.Simulator.SevereWeatherLocationIds,
                },
                cancellationToken
            );
            if (journeyId != null)
            {
                await Track(creatureId, now, cancellationToken);
                journeys++;
                continue;
            }

            _loaded.Simulator.DeferUntilNextRoutineChange(creatureId, now);
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
        var count = Math.Min(_loaded.Simulator.Options.RoutinePlansPerTick, candidates.Count);
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

    private void LogMovementEvents(GameInstant now, IEnumerable<CreatureSimEvent> events)
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
                _applier.Enqueue(_loaded.Simulator.Engage(engage.CreatureId, now));
                break;
            case ReleaseCreature release:
                _applier.Enqueue(_loaded.Simulator.Release(release.CreatureId, now));
                break;
            case SpawnCreature spawn:
                _loaded.Simulator.Add(spawn.Seed, now);
                break;
            case RemoveCreature remove:
                _loaded.Simulator.Remove(remove.CreatureId);
                transientWalks.Remove(remove.CreatureId);
                break;
            case TrackCreature track:
                await Track(track.CreatureId, now, cancellationToken);
                break;
            case RefreshCreatureStatus refresh:
                UnpublishedCreatureIds(refresh.LocationId).Add(refresh.CreatureId);
                break;
            case RefreshPlayerScene refreshPlayer:
                subscriptions.RequestRefresh(worldId, refreshPlayer.PlayerId);
                break;
            case RefreshLocationScenes refreshLocation:
                subscriptions.RequestLocationRefresh(worldId, refreshLocation.LocationId);
                break;
            case PlayerInput input:
                await ApplyPlayerInput(input, cancellationToken);
                break;
            case PlayerDisconnected disconnected:
                await FlushPlayerPoses(cancellationToken);
                _playerPoses.Forget(disconnected.PlayerId);
                break;
            case ForgetPlayerPose forget:
                _playerPoses.Forget(forget.PlayerId);
                break;
            case SeedPlayerPose seed:
                await SeedPlayerPose(seed.PlayerId, cancellationToken);
                break;
            case InvalidateMovementSpace invalidate:
                _movementSpaces.Invalidate(invalidate.LocationId);
                break;
            case SimulationRequest request:
                await request.Run(_playerPoses, cancellationToken);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(message));
        }
    }

    private async Task ApplyPlayerInput(PlayerInput input, CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = serviceScopeFactory.CreateAsyncScope();
            var space = await _movementSpaces.Find(
                input.LocationId,
                scope.ServiceProvider,
                cancellationToken
            );
            if (space is null)
            {
                return;
            }

            var pose = await scope
                .ServiceProvider.GetRequiredService<
                    ICommandHandler<ApplyPlayerInputCommand, PlayerPose?>
                >()
                .Handle(
                    new ApplyPlayerInputCommand
                    {
                        WorldId = worldId,
                        PlayerId = input.PlayerId,
                        LocationId = input.LocationId,
                        Input = input.Input,
                        ClientPosition = input.ClientPosition,
                        ReceivedAt = input.ReceivedAt,
                        Space = space,
                        Previous = _playerPoses.Find(input.PlayerId),
                    },
                    cancellationToken
                );
            await scope
                .ServiceProvider.GetRequiredService<IGameClientEventDispatcher>()
                .FlushAsync(worldId, cancellationToken);
            if (pose is not null)
            {
                _playerPoses.Set(input.PlayerId, pose);
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(
                exception,
                "Dropped player input for {PlayerId} in world {WorldId}",
                input.PlayerId,
                worldId
            );
        }
    }

    private async Task SeedPlayerPose(Guid playerId, CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = serviceScopeFactory.CreateAsyncScope();
            var settled = await scope
                .ServiceProvider.GetRequiredService<
                    IQueryHandler<GetSettledPlayerPoseQuery, SettledPlayerPose?>
                >()
                .Handle(new GetSettledPlayerPoseQuery { PlayerId = playerId }, cancellationToken);
            if (settled is null || _playerPoses.Find(playerId)?.LocationId == settled.LocationId)
            {
                return;
            }

            _playerPoses.Seed(playerId, settled.ToIdlePose());
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(
                exception,
                "Could not seed pose for {PlayerId} in world {WorldId}",
                playerId,
                worldId
            );
        }
    }

    private async Task Track(Guid creatureId, GameInstant now, CancellationToken cancellationToken)
    {
        await using var scope = serviceScopeFactory.CreateAsyncScope();
        var seed = await scope
            .ServiceProvider.GetRequiredService<WorldSimulatorLoader>()
            .LoadSeed(worldId, creatureId, cancellationToken);

        _loaded.Simulator.Remove(creatureId);
        if (seed != null)
        {
            _loaded.Simulator.Add(seed, now);
        }
    }

    private async Task<bool> EnsureWritable(GameInstant now, CancellationToken cancellationToken)
    {
        if (!await EnsureHealthy(now, cancellationToken))
        {
            return false;
        }

        return !_applier.IsBlocked || await TryCommit(now, cancellationToken);
    }

    private async Task<bool> TryCommit(GameInstant now, CancellationToken cancellationToken)
    {
        if (!_applier.HasPending)
        {
            return true;
        }

        try
        {
            var committed = await _applier.Commit(cancellationToken);
            OnCommitted(committed, now);

            return true;
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogError(
                exception,
                "Simulation batch write failed in world {WorldId} (failure {Failures} of {Maximum})",
                worldId,
                _applier.ConsecutiveFailures,
                options.MaximumConsecutiveBatchFailures
            );
            if (_applier.RetriesExhausted)
            {
                await BeginRecovery(now, cancellationToken);
            }

            return false;
        }
    }

    private async Task BeginRecovery(GameInstant now, CancellationToken cancellationToken)
    {
        _isRecovering = true;
        subscriptions.MarkWorldForResync(worldId);
        await EnsureHealthy(now, cancellationToken);
    }

    private void OnCommitted(IReadOnlyList<SimEvent> committed, GameInstant now)
    {
        if (committed.Count == 0)
        {
            return;
        }

        LogMovementEvents(now, committed.OfType<CreatureSimEvent>());
        _unexecutedArrivals.AddRange(committed.OfType<JourneyCompleted>());
        _unexecutedLocalArrivals.AddRange(committed.OfType<LocalMoveCompleted>());
        _unpublishedVitals.AddRange(committed.OfType<CreatureVitalsEvent>());
        NoteWeatherChanges(committed);
        UpdateTransientWalks(committed);
        RememberUnpublishedCreatures(committed);
    }

    private void NoteWeatherChanges(IReadOnlyList<SimEvent> committed)
    {
        if (committed.OfType<WeatherChanged>().Any())
        {
            _weatherUnannounced = true;
            _exposureStale = true;
        }
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
            _applier.Enqueue(_loaded.Simulator.StartLocalMove(move, arrivalAt));
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
                _applier.Enqueue(_loaded.Simulator.StartLocalMove(retry, arrival.At));
            }
        }
    }

    private void UpdateTransientWalks(IReadOnlyList<SimEvent> committed)
    {
        foreach (var simEvent in committed)
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
        if (
            _unpublishedCreatureIdsByLocationId.Count == 0
            && _unpublishedVitals.Count == 0
            && !_weatherUnannounced
        )
        {
            return;
        }

        await using var scope = serviceScopeFactory.CreateAsyncScope();
        await PublishVitals(scope.ServiceProvider, cancellationToken);
        await PublishCreatureChanges(scope.ServiceProvider, now, cancellationToken);
        await PublishWeather(scope.ServiceProvider, cancellationToken);

        // Flushing inside the lease keeps the scene ordered with the mutations it describes.
        await scope
            .ServiceProvider.GetRequiredService<IGameClientEventDispatcher>()
            .FlushAsync(worldId, cancellationToken);
    }

    private async Task PublishWeather(
        IServiceProvider services,
        CancellationToken cancellationToken
    )
    {
        if (!_weatherUnannounced)
        {
            return;
        }

        await services.GetRequiredService<WeatherAnnouncer>().Announce(worldId, cancellationToken);
        _weatherUnannounced = false;
    }

    private async Task PublishVitals(IServiceProvider services, CancellationToken cancellationToken)
    {
        if (_unpublishedVitals.Count == 0)
        {
            return;
        }

        await services
            .GetRequiredService<VitalsAnnouncer>()
            .Announce(worldId, _unpublishedVitals, cancellationToken);
        _unpublishedVitals.Clear();
    }

    private async Task PublishCreatureChanges(
        IServiceProvider services,
        GameInstant now,
        CancellationToken cancellationToken
    )
    {
        if (_unpublishedCreatureIdsByLocationId.Count == 0)
        {
            return;
        }

        await services
            .GetRequiredService<CreatureChangeAnnouncer>()
            .Announce(
                worldId,
                now,
                _unpublishedCreatureIdsByLocationId.ToDictionary(
                    entry => entry.Key,
                    entry => (IReadOnlySet<Guid>)entry.Value
                ),
                cancellationToken
            );
        await services
            .GetRequiredService<PropOccupancyAnnouncer>()
            .Announce(
                worldId,
                _unpublishedCreatureIdsByLocationId.Keys.ToArray(),
                cancellationToken
            );
        _unpublishedCreatureIdsByLocationId.Clear();
    }

    private void RememberUnpublishedCreatures(IReadOnlyList<SimEvent> committed)
    {
        foreach (var simEvent in committed.OfType<CreatureSimEvent>())
        {
            foreach (var locationId in SimEventLocations.Touched(simEvent))
            {
                UnpublishedCreatureIds(locationId).Add(simEvent.CreatureId);
            }
        }
    }

    private HashSet<Guid> UnpublishedCreatureIds(Guid locationId)
    {
        if (!_unpublishedCreatureIdsByLocationId.TryGetValue(locationId, out var creatureIds))
        {
            creatureIds = [];
            _unpublishedCreatureIdsByLocationId[locationId] = creatureIds;
        }

        return creatureIds;
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
        public static MovementDiagnostic From(CreatureSimEvent simEvent) =>
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
