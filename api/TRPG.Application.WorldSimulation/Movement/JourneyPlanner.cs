using TRPG.Application.Common.Navigation;
using TRPG.Domain;

namespace TRPG.Application.WorldSimulation.Movement;

internal sealed class JourneyPlanner(RouteFinder routeFinder, WeatherShelter shelter)
{
    private static readonly TimeSpan RetryWhenUnreachable = TimeSpan.FromHours(1);
    private static readonly TimeSpan RetryWhenNothingScheduled = TimeSpan.FromDays(7);

    public void Plan(SimulatedCreature creature, GameInstant now)
    {
        var transition = JobTransition.FindNext(
            creature.Jobs,
            creature.LocationId,
            now,
            job => shelter.Apply(creature, job),
            job => creature.PatrolFor(job) != null
        );
        if (transition == null)
        {
            creature.NextUpdate = now + RetryWhenNothingScheduled;
            return;
        }

        if (
            creature.PatrolFor(transition.Destination) is { } patrol
            && transition.Destination.LocationId == creature.LocationId
        )
        {
            PlanPatrol(creature, transition, patrol, now);
            return;
        }

        if (
            !routeFinder.TryFind(
                creature.LocationId,
                transition.Destination.LocationId,
                out var legs
            )
        )
        {
            return;
        }

        if (legs.Count == 0)
        {
            creature.NextUpdate = now + RetryWhenUnreachable;
            return;
        }

        creature.Journey = new Journey(legs, transition.Destination, transition.At);
        creature.NextUpdate = ResolveDeparture(
            creature,
            transition,
            legs.Sum(leg => leg.Distance),
            now
        );
    }

    private static void PlanPatrol(
        SimulatedCreature creature,
        JobTransition transition,
        IReadOnlyList<RouteLeg> patrol,
        GameInstant now
    )
    {
        var start = transition.IsOpen ? now : transition.At;
        creature.Journey = new Journey(
            patrol,
            transition.Destination,
            transition.At,
            JobTransition.FindWindowEnd(creature.Jobs, transition.Destination, start)
        );
        creature.NextUpdate = start;
    }

    private static GameInstant ResolveDeparture(
        SimulatedCreature creature,
        JobTransition transition,
        double totalMeters,
        GameInstant now
    )
    {
        if (transition.IsOpen)
        {
            return now;
        }

        var walkDuration = TimeSpan.FromSeconds(totalMeters / creature.MetersPerGameSecond);
        var departure = DepartureTiming.Resolve(creature.Id, transition, walkDuration);

        return departure > now ? departure : now;
    }
}
