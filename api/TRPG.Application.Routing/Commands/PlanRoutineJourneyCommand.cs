using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Navigation;
using TRPG.Application.Common.Queries;
using TRPG.Application.CreatureJobs;
using TRPG.Application.Worlds.Queries;
using TRPG.Data.ModuleContexts;
using TRPG.Domain;
using TRPG.Domain.Models;

namespace TRPG.Application.Routing.Commands;

public class PlanRoutineJourneyCommand
{
    public required Guid CreatureId { get; init; }
    public required GameInstant PlannedAt { get; init; }
    public required double TimeScale { get; init; }
    public required TimeSpan ArrivalStagger { get; init; }
}

internal class PlanRoutineJourneyCommandHandler(
    ICreaturesDbContext creatures,
    ICreatureJobsDbContext creatureJobs,
    IRoutingDbContext routing,
    IQueryHandler<GetTravelGraphQuery, TravelGraph> getTravelGraph
) : ICommandHandler<PlanRoutineJourneyCommand, Guid?>
{
    public async Task<Guid?> Handle(
        PlanRoutineJourneyCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var creature = await creatures.Creatures.FindAsync([command.CreatureId], cancellationToken);
        if (creature is null || await HasActiveJourney(creature.Id, cancellationToken))
        {
            return null;
        }

        var jobs = await creatureJobs
            .CreatureJobs.AsNoTracking()
            .Where(job => job.CreatureId == creature.Id)
            .OrderByDescending(job => job.Priority)
            .ToArrayAsync(cancellationToken);
        var transition = FindNextTransition(jobs, creature.LocationId, command.PlannedAt);
        if (transition is null)
        {
            return null;
        }

        var graph = await getTravelGraph.Handle(
            new GetTravelGraphQuery { WorldId = creature.WorldId },
            cancellationToken
        );
        var originNodeId = ResolveOriginNode(creature, graph);
        if (originNodeId is null)
        {
            return null;
        }

        var target = ResolveTarget(transition.Destination, graph, originNodeId.Value);
        if (target is null)
        {
            return null;
        }

        var journey = CreateJourney(creature, transition, target, command);
        creature.CurrentTravelNodeId = originNodeId;
        routing.Journeys.Add(journey);
        routing.JourneyMembers.Add(
            new JourneyMember { JourneyId = journey.Id, CreatureId = creature.Id }
        );
        routing.JourneyLegs.AddRange(CreateLegs(journey.Id, target.Legs));
        await routing.SaveChangesAsync(cancellationToken);

        return journey.Id;
    }

    private Task<bool> HasActiveJourney(Guid creatureId, CancellationToken cancellationToken) =>
        routing.JourneyMembers.AnyAsync(
            member =>
                member.CreatureId == creatureId
                && routing.Journeys.Any(journey =>
                    journey.Id == member.JourneyId
                    && (
                        journey.Status == JourneyStatus.Planned
                        || journey.Status == JourneyStatus.Traveling
                        || journey.Status == JourneyStatus.Dwelling
                    )
                ),
            cancellationToken
        );

    private static Guid? ResolveOriginNode(Creature creature, TravelGraph graph)
    {
        if (creature.CurrentTravelNodeId is { } nodeId)
        {
            return nodeId;
        }

        return graph.FindNearestNode(creature.LocationId, new Point(creature.X, creature.Y));
    }

    private static JourneyTarget? ResolveTarget(
        CreatureJob job,
        TravelGraph graph,
        Guid originNodeId
    )
    {
        var nodeTargets = graph.NodeIdsAt(job.LocationId);

        return BestReachable(graph, originNodeId, nodeTargets);
    }

    private static JourneyTarget? BestReachable(
        TravelGraph graph,
        Guid originNodeId,
        IEnumerable<Guid> targets
    ) =>
        targets
            .Select(target => new JourneyTarget(
                target,
                graph.FindShortestPath(originNodeId, target)
            ))
            .Where(target => target.Legs.Count > 0 || target.NodeId == originNodeId)
            .OrderBy(target => target.Legs.Sum(leg => leg.Distance))
            .FirstOrDefault();

    private static Journey CreateJourney(
        Creature creature,
        RoutineTransition transition,
        JourneyTarget target,
        PlanRoutineJourneyCommand command
    )
    {
        var totalDistance = target.Legs.Sum(leg => leg.Distance);
        var duration = TimeSpan.FromSeconds(
            totalDistance
                / InLocationPace.MetersPerGameSecond(creature.MovementSpeed, command.TimeScale)
        );
        var departure = ResolveDeparture(creature.Id, transition, duration, command.ArrivalStagger);

        return new Journey
        {
            WorldId = creature.WorldId,
            DestinationJobId = transition.Destination.Id,
            ArrivalActivity = transition.Destination.Activity,
            Status = JourneyStatus.Planned,
            PlannedAt = command.PlannedAt,
            DepartureAt = departure > command.PlannedAt ? departure : command.PlannedAt,
            CheckpointLegIndex = 0,
            CheckpointLegProgressMeters = 0,
            CheckpointedAt = command.PlannedAt,
        };
    }

    private static IReadOnlyCollection<JourneyLeg> CreateLegs(
        Guid journeyId,
        IReadOnlyList<DirectedTravelLeg> legs
    ) =>
        legs.Select(
                (leg, index) =>
                    new JourneyLeg
                    {
                        JourneyId = journeyId,
                        Index = index,
                        FromNodeId = leg.FromNodeId,
                        ToNodeId = leg.ToNodeId,
                        ConnectorId = leg.ConnectorId,
                        Distance = leg.Distance,
                        Path = CopyPath(leg.Path),
                        DwellAfter = TimeSpan.Zero,
                    }
            )
            .ToArray();

    private static Polyline CopyPath(Polyline path) =>
        new() { Points = [.. path.Points.Select(point => new Point(point.X, point.Y))] };

    private static RoutineTransition? FindNextTransition(
        IReadOnlyList<CreatureJob> jobs,
        Guid currentLocationId,
        GameInstant now
    )
    {
        var firstBoundary = new GameInstant(now.Value.Date.AddHours(now.Value.Hour));
        for (var offset = 0; offset <= 7 * 24; offset++)
        {
            var at = offset == 0 ? now : firstBoundary + TimeSpan.FromHours(offset);
            var destination = CreatureJobScheduling.FindDueJob(
                jobs,
                at.Value.DayOfWeek,
                at.Value.Hour
            );
            if (destination is not null && destination.LocationId != currentLocationId)
            {
                var origin = CreatureJobScheduling.FindDueJob(
                    jobs,
                    (at - TimeSpan.FromHours(1)).Value.DayOfWeek,
                    (at - TimeSpan.FromHours(1)).Value.Hour
                );
                return new RoutineTransition(destination, origin, at, offset == 0);
            }
        }

        return null;
    }

    private static GameInstant ResolveDeparture(
        Guid creatureId,
        RoutineTransition transition,
        TimeSpan duration,
        TimeSpan arrivalStagger
    )
    {
        if (transition.IsOpen)
        {
            return transition.At;
        }

        var jitter = StableUnitInterval(creatureId, transition);
        if (transition.Destination.Action == CreatureJobAction.Eat)
        {
            return transition.At - duration + TimeSpan.FromHours((jitter * 2 - 1) / 3);
        }

        if (transition.Origin?.Action == CreatureJobAction.Work)
        {
            return transition.At + TimeSpan.FromHours(jitter / 2);
        }

        return transition.At - duration - arrivalStagger * jitter;
    }

    private static double StableUnitInterval(Guid creatureId, RoutineTransition transition)
    {
        const ulong offset = 14695981039346656037;
        const ulong prime = 1099511628211;
        var weekHour = (int)transition.At.Value.DayOfWeek * 24 + transition.At.Value.Hour;
        var hash = offset;
        foreach (
            var value in creatureId
                .ToByteArray()
                .Concat((transition.Origin?.Id ?? Guid.Empty).ToByteArray())
                .Concat(transition.Destination.Id.ToByteArray())
                .Concat(BitConverter.GetBytes(weekHour))
        )
        {
            hash = unchecked((hash ^ value) * prime);
        }

        return (hash >> 11) * (1.0 / (1UL << 53));
    }

    private sealed record JourneyTarget(Guid NodeId, IReadOnlyList<DirectedTravelLeg> Legs);

    private sealed record RoutineTransition(
        CreatureJob Destination,
        CreatureJob? Origin,
        GameInstant At,
        bool IsOpen
    );
}
