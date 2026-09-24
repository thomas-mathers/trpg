using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Queries;
using TRPG.Application.CreatureJobs;
using TRPG.Application.CreatureJobs.Commands;
using TRPG.Application.CreatureJobs.Queries;
using TRPG.Application.Creatures.Commands;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.Props.Commands;
using TRPG.Application.Routing.Commands;
using TRPG.Application.Routing.Queries;
using TRPG.Application.Worlds.Queries;
using TRPG.Domain;
using TRPG.Domain.Models;

namespace TRPG.Application.LocationSimulation.Commands;

public class SyncCreatureJobSchedulesCommand
{
    public required IReadOnlyCollection<Guid> CreatureIds { get; init; }
    public required TimeSpan Playtime { get; init; }
    public WeatherCondition? Weather { get; init; }
    public IReadOnlyDictionary<
        Guid,
        TimeSpan
    > BecameAvailableAtPlaytimeByCreatureId { get; init; } = new Dictionary<Guid, TimeSpan>();
}

public record SyncCreatureJobSchedulesResult(
    IReadOnlyDictionary<Guid, IReadOnlyList<Guid>> WorkingCreatureIdsByLocationId
);

internal class SyncCreatureJobSchedulesCommandHandler(
    IQueryHandler<
        GetCreatureJobsByCreatureIdsQuery,
        IReadOnlyDictionary<Guid, IReadOnlyList<CreatureJob>>
    > getJobsByCreatureIds,
    IQueryHandler<GetCreaturesByIdsQuery, IReadOnlyDictionary<Guid, Creature>> getCreaturesByIds,
    IQueryHandler<
        GetCreatureRoutePositionsQuery,
        IReadOnlyDictionary<Guid, CreatureRoutePosition>
    > getCreatureRoutePositions,
    IQueryHandler<
        GetRouteTravelDurationsQuery,
        IReadOnlyDictionary<Guid, TimeSpan>
    > getRouteTravelDurations,
    IQueryHandler<GetLocationsByIdsQuery, IReadOnlyDictionary<Guid, Location>> getLocationsByIds,
    ICommandHandler<UpdateCreaturesCommand> updateCreatures,
    ICommandHandler<CompleteCreatureRoutesCommand> completeCreatureRoutes,
    ICommandHandler<
        RouteCreaturesToDestinationsCommand,
        IReadOnlyDictionary<Guid, RouteCreatureResult>
    > routeCreaturesToDestinations,
    ICommandHandler<StartCreaturesOnRoutesCommand> startCreaturesOnRoutes,
    ICommandHandler<ExecuteCreatureJobCommand> executeCreatureJob,
    ICommandHandler<ClearBedOccupantsCommand> clearBedOccupants,
    ICommandHandler<ClearWorkstationOccupantsCommand> clearWorkstationOccupants,
    ICommandHandler<ClearSeatOccupantsCommand> clearSeatOccupants
) : ICommandHandler<SyncCreatureJobSchedulesCommand, SyncCreatureJobSchedulesResult>
{
    private static readonly HashSet<CreatureState> NonSchedulableStates =
    [
        CreatureState.Alerted,
        CreatureState.Dead,
        CreatureState.Restrained,
    ];

    public async Task<SyncCreatureJobSchedulesResult> Handle(
        SyncCreatureJobSchedulesCommand command,
        CancellationToken cancellationToken = default
    )
    {
        if (command.CreatureIds.Count == 0)
        {
            return EmptyResult();
        }

        ValidateAvailability(command);

        var jobsByCreatureId = await getJobsByCreatureIds.Handle(
            new GetCreatureJobsByCreatureIdsQuery { CreatureIds = command.CreatureIds },
            cancellationToken
        );
        var scheduledIds = jobsByCreatureId.Keys.ToArray();
        if (scheduledIds.Length == 0)
        {
            return EmptyResult();
        }

        var creaturesById = await getCreaturesByIds.Handle(
            new GetCreaturesByIdsQuery { Ids = scheduledIds },
            cancellationToken
        );
        var jobLocationsById = await getLocationsByIds.Handle(
            new GetLocationsByIdsQuery
            {
                Ids = jobsByCreatureId
                    .Values.SelectMany(creatureJobs => creatureJobs)
                    .Select(job => job.LocationId)
                    .Distinct()
                    .ToArray(),
            },
            cancellationToken
        );
        await clearBedOccupants.Handle(
            new ClearBedOccupantsCommand { CreatureIds = scheduledIds },
            cancellationToken
        );
        await clearWorkstationOccupants.Handle(
            new ClearWorkstationOccupantsCommand { CreatureIds = scheduledIds },
            cancellationToken
        );
        await clearSeatOccupants.Handle(
            new ClearSeatOccupantsCommand { CreatureIds = scheduledIds },
            cancellationToken
        );
        var routePositions = await getCreatureRoutePositions.Handle(
            new GetCreatureRoutePositionsQuery
            {
                CreatureIds = scheduledIds,
                Playtime = command.Playtime,
            },
            cancellationToken
        );

        var synchronization = ResolveCurrentPositions(
            creaturesById,
            routePositions,
            jobsByCreatureId,
            command.Playtime
        );
        synchronization = ApplyAvailability(
            synchronization,
            command.BecameAvailableAtPlaytimeByCreatureId
        );
        await PersistTargets(synchronization.Targets, cancellationToken);
        await completeCreatureRoutes.Handle(
            new CompleteCreatureRoutesCommand { CreatureIds = synchronization.ArrivedCreatureIds },
            cancellationToken
        );

        var decisions = await BuildScheduleDecisions(
            synchronization.ReadyCreatures,
            jobsByCreatureId,
            command.Playtime,
            command.Weather,
            jobLocationsById,
            cancellationToken
        );
        await PersistTargets(decisions.IdleTargets, cancellationToken);
        await StartRoutes(decisions.Routes, cancellationToken);
        var arrivals = await ResolveStartedRoutes(
            decisions.Routes,
            command.Playtime,
            creaturesById,
            cancellationToken
        );
        await PersistTargets(arrivals.Targets, cancellationToken);
        await completeCreatureRoutes.Handle(
            new CompleteCreatureRoutesCommand { CreatureIds = arrivals.CreatureIds },
            cancellationToken
        );

        var creaturesReadyForJobs = decisions
            .CreaturesAtDestination.Concat(arrivals.CreaturesAtDestination)
            .ToArray();
        return await ExecuteCurrentJobs(
            creaturesReadyForJobs,
            jobsByCreatureId,
            command.Playtime,
            cancellationToken
        );
    }

    private static void ValidateAvailability(SyncCreatureJobSchedulesCommand command)
    {
        if (
            command.BecameAvailableAtPlaytimeByCreatureId.Keys.Any(creatureId =>
                !command.CreatureIds.Contains(creatureId)
            )
        )
        {
            throw new ArgumentException(
                "Availability can only be supplied for synchronized creatures.",
                nameof(command)
            );
        }
    }

    private static PositionSynchronization ApplyAvailability(
        PositionSynchronization synchronization,
        IReadOnlyDictionary<Guid, TimeSpan> becameAvailableAtByCreatureId
    ) =>
        synchronization with
        {
            ReadyCreatures = synchronization
                .ReadyCreatures.Select(ready =>
                    becameAvailableAtByCreatureId.TryGetValue(
                        ready.Creature.Id,
                        out var becameAvailableAt
                    )
                    && (
                        ready.AvailableAtPlaytime == null
                        || becameAvailableAt > ready.AvailableAtPlaytime
                    )
                        ? ready with
                        {
                            AvailableAtPlaytime = becameAvailableAt,
                        }
                        : ready
                )
                .ToArray(),
        };

    private static PositionSynchronization ResolveCurrentPositions(
        IReadOnlyDictionary<Guid, Creature> creaturesById,
        IReadOnlyDictionary<Guid, CreatureRoutePosition> routePositions,
        IReadOnlyDictionary<Guid, IReadOnlyList<CreatureJob>> jobsByCreatureId,
        TimeSpan playtime
    )
    {
        var targets = new Dictionary<CreatureTarget, List<Guid>>();
        var arrivedIds = new List<Guid>();
        var ready = new List<ReadyCreature>();
        foreach (var creature in creaturesById.Values)
        {
            if (NonSchedulableStates.Contains(creature.State))
            {
                continue;
            }

            if (!routePositions.TryGetValue(creature.Id, out var routePosition))
            {
                ready.Add(
                    new ReadyCreature(creature, creature.LocationId, AvailableAtPlaytime: null)
                );
                continue;
            }

            var routeJob = jobsByCreatureId[creature.Id]
                .FirstOrDefault(job => job.RouteId == routePosition.RouteId);
            if (routePosition.Traversal == RouteTraversal.Cyclic && routeJob != null)
            {
                var currentDate = GameClock.GetCurrentInGameDate(playtime);
                var dueJob = CreatureJobScheduling.FindDueJob(
                    jobsByCreatureId[creature.Id],
                    currentDate.Weekday,
                    currentDate.Hour
                );
                if (dueJob?.Id != routeJob.Id)
                {
                    ready.Add(
                        new ReadyCreature(
                            creature,
                            creature.LocationId,
                            CreatureJobScheduling.FindMostRecentEndPlaytime(routeJob, playtime),
                            HasActiveRoute: true
                        )
                    );
                    continue;
                }

                AddCyclicRouteTarget(targets, routePosition.Position, creature.Id);
                continue;
            }

            switch (routePosition.Position)
            {
                case RouteTimelinePosition.Pending pending:
                    AddTarget(targets, pending.LocationId, CreatureState.Idle, creature.Id);
                    break;
                case RouteTimelinePosition.Lingering lingering:
                    AddTarget(targets, lingering.LocationId, CreatureState.Idle, creature.Id);
                    break;
                case RouteTimelinePosition.InTransit inTransit:
                    AddTarget(
                        targets,
                        inTransit.FromLocationId,
                        CreatureState.Walking,
                        creature.Id
                    );
                    break;
                case RouteTimelinePosition.Arrived arrived:
                    AddTarget(targets, arrived.LocationId, CreatureState.Idle, creature.Id);
                    arrivedIds.Add(creature.Id);
                    ready.Add(
                        new ReadyCreature(creature, arrived.LocationId, arrived.ArrivedAtPlaytime)
                    );
                    break;
            }
        }
        return new PositionSynchronization(targets, arrivedIds, ready);
    }

    private static void AddCyclicRouteTarget(
        Dictionary<CreatureTarget, List<Guid>> targets,
        RouteTimelinePosition position,
        Guid creatureId
    )
    {
        switch (position)
        {
            case RouteTimelinePosition.Pending pending:
                AddTarget(targets, pending.LocationId, CreatureState.Idle, creatureId);
                break;
            case RouteTimelinePosition.Lingering lingering:
                AddTarget(targets, lingering.LocationId, CreatureState.Busy, creatureId);
                break;
            case RouteTimelinePosition.InTransit inTransit:
                AddTarget(targets, inTransit.FromLocationId, CreatureState.Walking, creatureId);
                break;
            case RouteTimelinePosition.Arrived:
                throw new InvalidOperationException("A cyclic route cannot arrive.");
        }
    }

    private async Task<ScheduleDecisions> BuildScheduleDecisions(
        IReadOnlyCollection<ReadyCreature> readyCreatures,
        IReadOnlyDictionary<Guid, IReadOnlyList<CreatureJob>> jobsByCreatureId,
        TimeSpan playtime,
        WeatherCondition? weather,
        IReadOnlyDictionary<Guid, Location> locationsById,
        CancellationToken cancellationToken
    )
    {
        var scheduledCreatures = readyCreatures
            .Select(ready =>
                ResolveScheduledCreature(ready, jobsByCreatureId, playtime, weather, locationsById)
            )
            .Where(entry => entry != null)
            .Select(entry => entry!)
            .ToArray();
        var routesToMeasure = scheduledCreatures
            .Where(entry =>
                !entry.Ready.HasActiveRoute
                && entry.Ready.LocationId != entry.Scheduled.Job.LocationId
            )
            .Select(entry => new RouteTravelDurationRequest(
                entry.Ready.Creature.Id,
                entry.Ready.LocationId,
                entry.Scheduled.Job.LocationId,
                entry.Ready.Creature.MovementSpeed
            ))
            .ToArray();
        var durations =
            routesToMeasure.Length == 0
                ? new Dictionary<Guid, TimeSpan>()
                : await getRouteTravelDurations.Handle(
                    new GetRouteTravelDurationsQuery
                    {
                        WorldId = scheduledCreatures[0].Ready.Creature.WorldId,
                        Routes = routesToMeasure,
                    },
                    cancellationToken
                );

        var idleTargets = new Dictionary<CreatureTarget, List<Guid>>();
        var routes = new List<CreatureRoutePlan>();
        var atDestination = new List<CreatureAtDestination>();
        foreach (var entry in scheduledCreatures)
        {
            var ready = entry.Ready;
            var scheduled = entry.Scheduled;
            if (!ready.HasActiveRoute && ready.LocationId == scheduled.Job.LocationId)
            {
                AddTarget(idleTargets, ready.LocationId, CreatureState.Idle, ready.Creature.Id);
                atDestination.Add(
                    new CreatureAtDestination(
                        ready.Creature,
                        ready.LocationId,
                        ready.AvailableAtPlaytime
                    )
                );
                continue;
            }

            var plannedDeparture = ready.HasActiveRoute
                ? ready.AvailableAtPlaytime!.Value
                : scheduled.StartsAtPlaytime - durations[ready.Creature.Id];
            var departure =
                ready.AvailableAtPlaytime is { } available && available > plannedDeparture
                    ? available
                    : plannedDeparture;
            if (departure > playtime)
            {
                AddTarget(idleTargets, ready.LocationId, CreatureState.Idle, ready.Creature.Id);
                continue;
            }

            routes.Add(
                new CreatureRoutePlan(
                    ready.Creature,
                    ready.LocationId,
                    scheduled.Job.LocationId,
                    departure,
                    entry.Purpose
                )
            );
        }
        return new ScheduleDecisions(idleTargets, routes, atDestination);
    }

    private static ScheduledReadyCreature? ResolveScheduledCreature(
        ReadyCreature ready,
        IReadOnlyDictionary<Guid, IReadOnlyList<CreatureJob>> jobsByCreatureId,
        TimeSpan playtime,
        WeatherCondition? weather,
        IReadOnlyDictionary<Guid, Location> locationsById
    )
    {
        var decisionPlaytime = ready.AvailableAtPlaytime ?? playtime;
        var scheduled = CreatureJobScheduling.FindCurrentOrNextJob(
            jobsByCreatureId[ready.Creature.Id],
            decisionPlaytime
        );
        if (
            scheduled?.Job.LocationId == ready.LocationId
            && ready.AvailableAtPlaytime is { } available
            && available < playtime
        )
        {
            scheduled = CreatureJobScheduling.FindCurrentOrNextJob(
                jobsByCreatureId[ready.Creature.Id],
                playtime
            );
        }
        if (scheduled == null)
        {
            return null;
        }

        return ApplyWeatherSubstitution(
            ready,
            scheduled,
            jobsByCreatureId[ready.Creature.Id],
            weather,
            locationsById
        );
    }

    private static ScheduledReadyCreature ApplyWeatherSubstitution(
        ReadyCreature ready,
        CreatureJobScheduling.ScheduledCreatureJob scheduled,
        IReadOnlyCollection<CreatureJob> jobs,
        WeatherCondition? weather,
        IReadOnlyDictionary<Guid, Location> locationsById
    )
    {
        if (ShouldShelter(ready.Creature, scheduled.Job, weather, locationsById))
        {
            var sleepJob = jobs.FirstOrDefault(job => job.Action == CreatureJobAction.Sleep);
            if (sleepJob != null)
            {
                scheduled = scheduled with
                {
                    Job = CopyWithLocation(scheduled.Job, sleepJob.LocationId),
                };
                return new ScheduledReadyCreature(ready, scheduled, "Going home to take shelter");
            }
        }

        return new ScheduledReadyCreature(ready, scheduled, PurposeFor(scheduled.Job.Action));
    }

    private static bool ShouldShelter(
        Creature creature,
        CreatureJob job,
        WeatherCondition? weather,
        IReadOnlyDictionary<Guid, Location> locationsById
    ) =>
        WeatherConditions.PreventsOptionalTravel(weather)
        && creature.Profession != Profession.Guard
        && job.Action == CreatureJobAction.Idle
        && locationsById.TryGetValue(job.LocationId, out var location)
        && location.Kind != LocationKind.Room;

    private static CreatureJob CopyWithLocation(CreatureJob job, Guid locationId) =>
        new()
        {
            Id = job.Id,
            Action = job.Action,
            CreatureId = job.CreatureId,
            EndHour = job.EndHour,
            LocationId = locationId,
            Priority = job.Priority,
            RouteId = job.RouteId,
            SpecificDay = job.SpecificDay,
            StartHour = job.StartHour,
            WorldId = job.WorldId,
        };

    private async Task<IReadOnlyDictionary<Guid, RouteCreatureResult>> StartRoutes(
        IReadOnlyCollection<CreatureRoutePlan> plans,
        CancellationToken cancellationToken
    )
    {
        if (plans.Count == 0)
        {
            return new Dictionary<Guid, RouteCreatureResult>();
        }

        return await routeCreaturesToDestinations.Handle(
            new RouteCreaturesToDestinationsCommand
            {
                Routes = plans
                    .Select(plan => new CreatureRouteRequest(
                        plan.Creature.Id,
                        plan.DestinationLocationId,
                        plan.StartedAtPlaytime,
                        plan.Purpose
                    ))
                    .ToArray(),
            },
            cancellationToken
        );
    }

    private async Task<ImmediateArrivals> ResolveStartedRoutes(
        IReadOnlyCollection<CreatureRoutePlan> plans,
        TimeSpan playtime,
        IReadOnlyDictionary<Guid, Creature> creaturesById,
        CancellationToken cancellationToken
    )
    {
        var targets = new Dictionary<CreatureTarget, List<Guid>>();
        var arrivedIds = new List<Guid>();
        var atDestination = new List<CreatureAtDestination>();
        var positions = await getCreatureRoutePositions.Handle(
            new GetCreatureRoutePositionsQuery
            {
                CreatureIds = plans.Select(plan => plan.Creature.Id).ToArray(),
                Playtime = playtime,
            },
            cancellationToken
        );
        foreach (var plan in plans)
        {
            var position = positions[plan.Creature.Id].Position;
            switch (position)
            {
                case RouteTimelinePosition.Pending pending:
                    AddTarget(targets, pending.LocationId, CreatureState.Idle, plan.Creature.Id);
                    break;
                case RouteTimelinePosition.Lingering lingering:
                    AddTarget(targets, lingering.LocationId, CreatureState.Idle, plan.Creature.Id);
                    break;
                case RouteTimelinePosition.InTransit inTransit:
                    AddTarget(
                        targets,
                        inTransit.FromLocationId,
                        CreatureState.Walking,
                        plan.Creature.Id
                    );
                    break;
                case RouteTimelinePosition.Arrived arrived:
                    AddTarget(targets, arrived.LocationId, CreatureState.Idle, plan.Creature.Id);
                    arrivedIds.Add(plan.Creature.Id);
                    atDestination.Add(
                        new CreatureAtDestination(
                            creaturesById[plan.Creature.Id],
                            arrived.LocationId,
                            arrived.ArrivedAtPlaytime
                        )
                    );
                    break;
            }
        }
        return new ImmediateArrivals(targets, arrivedIds, atDestination);
    }

    private async Task<SyncCreatureJobSchedulesResult> ExecuteCurrentJobs(
        IReadOnlyCollection<CreatureAtDestination> creatures,
        IReadOnlyDictionary<Guid, IReadOnlyList<CreatureJob>> jobsByCreatureId,
        TimeSpan playtime,
        CancellationToken cancellationToken
    )
    {
        var workingByLocationId = new Dictionary<Guid, List<Guid>>();
        var patrols = new List<StartCreatureOnRouteRequest>();
        var currentDate = GameClock.GetCurrentInGameDate(playtime);
        foreach (
            var entry in creatures
                .DistinctBy(entry => entry.Creature.Id)
                .OrderBy(entry => entry.AvailableAtPlaytime ?? TimeSpan.MinValue)
                .ThenBy(entry => entry.Creature.Id)
        )
        {
            var job = CreatureJobScheduling.FindDueJob(
                jobsByCreatureId[entry.Creature.Id],
                currentDate.Weekday,
                currentDate.Hour
            );
            if (job == null || job.LocationId != entry.LocationId)
            {
                continue;
            }

            if (job.RouteId != null)
            {
                var scheduled = CreatureJobScheduling.FindCurrentOrNextJob(
                    jobsByCreatureId[entry.Creature.Id],
                    playtime
                )!;
                var startedAt =
                    entry.AvailableAtPlaytime is { } available
                    && available > scheduled.StartsAtPlaytime
                        ? available
                        : scheduled.StartsAtPlaytime;
                patrols.Add(
                    new StartCreatureOnRouteRequest(
                        entry.Creature.Id,
                        job.RouteId.Value,
                        startedAt,
                        "Patrolling the city"
                    )
                );
                continue;
            }

            await executeCreatureJob.Handle(
                new ExecuteCreatureJobCommand
                {
                    CreatureId = entry.Creature.Id,
                    CurrentLocationId = entry.LocationId,
                    CurrentState = CreatureState.Idle,
                    CreatureJobAction = job.Action,
                    JobLocationId = job.LocationId,
                },
                cancellationToken
            );
            if (job.Action == CreatureJobAction.Work)
            {
                workingByLocationId.TryAdd(job.LocationId, []);
                workingByLocationId[job.LocationId].Add(entry.Creature.Id);
            }
        }

        await StartPatrols(patrols, playtime, cancellationToken);

        return new SyncCreatureJobSchedulesResult(
            workingByLocationId.ToDictionary(
                entry => entry.Key,
                entry => (IReadOnlyList<Guid>)entry.Value.ToArray()
            )
        );
    }

    private async Task StartPatrols(
        IReadOnlyCollection<StartCreatureOnRouteRequest> patrols,
        TimeSpan playtime,
        CancellationToken cancellationToken
    )
    {
        if (patrols.Count == 0)
        {
            return;
        }

        await startCreaturesOnRoutes.Handle(
            new StartCreaturesOnRoutesCommand { Routes = patrols },
            cancellationToken
        );
        var positions = await getCreatureRoutePositions.Handle(
            new GetCreatureRoutePositionsQuery
            {
                CreatureIds = patrols.Select(patrol => patrol.CreatureId).ToArray(),
                Playtime = playtime,
            },
            cancellationToken
        );
        var targets = new Dictionary<CreatureTarget, List<Guid>>();
        foreach (var (creatureId, position) in positions)
        {
            AddCyclicRouteTarget(targets, position.Position, creatureId);
        }
        await PersistTargets(targets, cancellationToken);
    }

    private async Task PersistTargets(
        IReadOnlyDictionary<CreatureTarget, List<Guid>> targets,
        CancellationToken cancellationToken
    )
    {
        foreach (var (target, creatureIds) in targets)
        {
            await updateCreatures.Handle(
                new UpdateCreaturesCommand
                {
                    CreatureIds = creatureIds,
                    LocationId = target.LocationId,
                    State = target.State,
                },
                cancellationToken
            );
        }
    }

    private static void AddTarget(
        Dictionary<CreatureTarget, List<Guid>> targets,
        Guid locationId,
        CreatureState state,
        Guid creatureId
    )
    {
        var target = new CreatureTarget(locationId, state);
        targets.TryAdd(target, []);
        targets[target].Add(creatureId);
    }

    private static string PurposeFor(CreatureJobAction action) =>
        action switch
        {
            CreatureJobAction.Sleep => "Walking home to sleep",
            CreatureJobAction.Work => "Walking to work",
            CreatureJobAction.Idle => "Walking to spend free time",
            CreatureJobAction.Study => "Walking to study",
            CreatureJobAction.Pray => "Walking to pray",
            CreatureJobAction.Eat => "Walking home to eat",
            _ => throw new ArgumentOutOfRangeException(nameof(action), action, null),
        };

    private static SyncCreatureJobSchedulesResult EmptyResult() =>
        new(new Dictionary<Guid, IReadOnlyList<Guid>>());

    private record CreatureTarget(Guid LocationId, CreatureState State);

    private record ReadyCreature(
        Creature Creature,
        Guid LocationId,
        TimeSpan? AvailableAtPlaytime,
        bool HasActiveRoute = false
    );

    private record ScheduledReadyCreature(
        ReadyCreature Ready,
        CreatureJobScheduling.ScheduledCreatureJob Scheduled,
        string Purpose
    );

    private record CreatureRoutePlan(
        Creature Creature,
        Guid OriginLocationId,
        Guid DestinationLocationId,
        TimeSpan StartedAtPlaytime,
        string Purpose
    );

    private record CreatureAtDestination(
        Creature Creature,
        Guid LocationId,
        TimeSpan? AvailableAtPlaytime
    );

    private record PositionSynchronization(
        IReadOnlyDictionary<CreatureTarget, List<Guid>> Targets,
        IReadOnlyCollection<Guid> ArrivedCreatureIds,
        IReadOnlyCollection<ReadyCreature> ReadyCreatures
    );

    private record ScheduleDecisions(
        IReadOnlyDictionary<CreatureTarget, List<Guid>> IdleTargets,
        IReadOnlyCollection<CreatureRoutePlan> Routes,
        IReadOnlyCollection<CreatureAtDestination> CreaturesAtDestination
    );

    private record ImmediateArrivals(
        IReadOnlyDictionary<CreatureTarget, List<Guid>> Targets,
        IReadOnlyCollection<Guid> CreatureIds,
        IReadOnlyCollection<CreatureAtDestination> CreaturesAtDestination
    );
}
