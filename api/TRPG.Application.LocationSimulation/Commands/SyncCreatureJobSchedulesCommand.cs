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
using TRPG.Domain;
using TRPG.Domain.Models;

namespace TRPG.Application.LocationSimulation.Commands;

public class SyncCreatureJobSchedulesCommand
{
    public required IReadOnlyCollection<Guid> CreatureIds { get; init; }
    public required TimeSpan Playtime { get; init; }
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
    ICommandHandler<UpdateCreaturesCommand> updateCreatures,
    ICommandHandler<CompleteCreatureRoutesCommand> completeCreatureRoutes,
    ICommandHandler<
        RouteCreaturesToDestinationsCommand,
        IReadOnlyDictionary<Guid, RouteCreatureResult>
    > routeCreaturesToDestinations,
    ICommandHandler<ExecuteCreatureJobCommand> executeCreatureJob,
    ICommandHandler<ClearBedOccupantsCommand> clearBedOccupants,
    ICommandHandler<ClearWorkstationOccupantsCommand> clearWorkstationOccupants
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
        await clearBedOccupants.Handle(
            new ClearBedOccupantsCommand { CreatureIds = scheduledIds },
            cancellationToken
        );
        await clearWorkstationOccupants.Handle(
            new ClearWorkstationOccupantsCommand { CreatureIds = scheduledIds },
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
            command.Playtime
        );
        await PersistTargets(synchronization.Targets, cancellationToken);
        await completeCreatureRoutes.Handle(
            new CompleteCreatureRoutesCommand { CreatureIds = synchronization.ArrivedCreatureIds },
            cancellationToken
        );

        var decisions = BuildScheduleDecisions(
            synchronization.ReadyCreatures,
            jobsByCreatureId,
            command.Playtime
        );
        await PersistTargets(decisions.IdleTargets, cancellationToken);
        var startedRoutes = await StartRoutes(decisions.Routes, cancellationToken);
        var arrivals = ResolveImmediateArrivals(
            decisions.Routes,
            startedRoutes,
            command.Playtime,
            creaturesById
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

    private static PositionSynchronization ResolveCurrentPositions(
        IReadOnlyDictionary<Guid, Creature> creaturesById,
        IReadOnlyDictionary<Guid, CreatureRoutePosition> routePositions,
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
                ready.Add(new ReadyCreature(creature, creature.LocationId, playtime));
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

    private static ScheduleDecisions BuildScheduleDecisions(
        IReadOnlyCollection<ReadyCreature> readyCreatures,
        IReadOnlyDictionary<Guid, IReadOnlyList<CreatureJob>> jobsByCreatureId,
        TimeSpan playtime
    )
    {
        var idleTargets = new Dictionary<CreatureTarget, List<Guid>>();
        var routes = new List<CreatureRoutePlan>();
        var atDestination = new List<CreatureAtDestination>();
        foreach (var ready in readyCreatures)
        {
            var scheduled = CreatureJobScheduling.FindCurrentOrNextJob(
                jobsByCreatureId[ready.Creature.Id],
                ready.DecisionPlaytime
            );
            if (scheduled?.Job.LocationId == ready.LocationId && ready.DecisionPlaytime < playtime)
            {
                scheduled = CreatureJobScheduling.FindCurrentOrNextJob(
                    jobsByCreatureId[ready.Creature.Id],
                    playtime
                );
            }
            if (scheduled == null)
            {
                continue;
            }

            if (ready.LocationId == scheduled.Job.LocationId)
            {
                AddTarget(idleTargets, ready.LocationId, CreatureState.Idle, ready.Creature.Id);
                atDestination.Add(new CreatureAtDestination(ready.Creature, ready.LocationId));
                continue;
            }

            routes.Add(
                new CreatureRoutePlan(
                    ready.Creature,
                    ready.LocationId,
                    scheduled.Job.LocationId,
                    ready.DecisionPlaytime,
                    PurposeFor(scheduled.Job.Action)
                )
            );
        }
        return new ScheduleDecisions(idleTargets, routes, atDestination);
    }

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

    private static ImmediateArrivals ResolveImmediateArrivals(
        IReadOnlyCollection<CreatureRoutePlan> plans,
        IReadOnlyDictionary<Guid, RouteCreatureResult> results,
        TimeSpan playtime,
        IReadOnlyDictionary<Guid, Creature> creaturesById
    )
    {
        var targets = new Dictionary<CreatureTarget, List<Guid>>();
        var arrivedIds = new List<Guid>();
        var atDestination = new List<CreatureAtDestination>();
        foreach (var plan in plans)
        {
            var result = results[plan.Creature.Id];
            if (result.ArrivesAtPlaytime <= playtime)
            {
                AddTarget(
                    targets,
                    plan.DestinationLocationId,
                    CreatureState.Idle,
                    plan.Creature.Id
                );
                arrivedIds.Add(plan.Creature.Id);
                atDestination.Add(
                    new CreatureAtDestination(
                        creaturesById[plan.Creature.Id],
                        plan.DestinationLocationId
                    )
                );
            }
            else
            {
                AddTarget(targets, plan.OriginLocationId, CreatureState.Walking, plan.Creature.Id);
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
        var currentDate = GameClock.GetCurrentInGameDate(playtime);
        foreach (var entry in creatures.DistinctBy(entry => entry.Creature.Id))
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

        return new SyncCreatureJobSchedulesResult(
            workingByLocationId.ToDictionary(
                entry => entry.Key,
                entry => (IReadOnlyList<Guid>)entry.Value.ToArray()
            )
        );
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
            _ => throw new ArgumentOutOfRangeException(nameof(action), action, null),
        };

    private static SyncCreatureJobSchedulesResult EmptyResult() =>
        new(new Dictionary<Guid, IReadOnlyList<Guid>>());

    private record CreatureTarget(Guid LocationId, CreatureState State);

    private record ReadyCreature(Creature Creature, Guid LocationId, TimeSpan DecisionPlaytime);

    private record CreatureRoutePlan(
        Creature Creature,
        Guid OriginLocationId,
        Guid DestinationLocationId,
        TimeSpan StartedAtPlaytime,
        string Purpose
    );

    private record CreatureAtDestination(Creature Creature, Guid LocationId);

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
