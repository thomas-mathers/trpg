using Microsoft.EntityFrameworkCore;
using TRPG.Application.WorldGeneration.Generators;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.WorldSimulation.LocalActivities;

internal sealed class LocalActivityPlanner(IPropsDbContext props, IWorldsDbContext worlds)
{
    public async Task<bool> IsSettled(
        Creature creature,
        CreatureJob job,
        CancellationToken cancellationToken
    )
    {
        if (job.Action == CreatureJobAction.Sleep)
        {
            return creature.Condition == CreatureCondition.Sleeping;
        }

        var localProps = await props
            .Props.AsNoTracking()
            .Where(prop => prop.LocationId == job.LocationId)
            .ToArrayAsync(cancellationToken);
        var occupiesTarget = localProps.Any(prop =>
            prop switch
            {
                Workstation workstation when job.Action == CreatureJobAction.Work =>
                    workstation.OccupantId == creature.Id,
                Seat seat when job.Action is CreatureJobAction.Work or CreatureJobAction.Idle =>
                    seat.OccupantId == creature.Id,
                _ => false,
            }
        );
        if (occupiesTarget)
        {
            return true;
        }

        return job.Action switch
        {
            CreatureJobAction.Work => creature.Activity == CreatureActivity.Working
                && !Candidates(localProps, creature.Id, job.Action).Any(),
            CreatureJobAction.Idle => creature.Activity == null
                && !Candidates(localProps, creature.Id, job.Action).Any(),
            _ => creature.Activity == job.Activity,
        };
    }

    public async Task<LocalMovePlan?> Plan(
        Creature creature,
        CreatureJob job,
        Point origin,
        CancellationToken cancellationToken
    )
    {
        var location = await worlds
            .Locations.AsNoTracking()
            .SingleAsync(location => location.Id == job.LocationId, cancellationToken);
        var localProps = await props
            .Props.AsNoTracking()
            .Where(prop => prop.LocationId == job.LocationId)
            .ToArrayAsync(cancellationToken);
        var buildings = await worlds
            .Buildings.AsNoTracking()
            .Where(building => building.ExteriorLocationId == job.LocationId)
            .ToArrayAsync(cancellationToken);
        var grid = LocationNavigationGrid.Build(location, localProps, buildings);

        return Candidates(localProps, creature.Id, job.Action)
            .Select(candidate => ToPlan(creature, job, origin, candidate, grid))
            .OfType<LocalMovePlan>()
            .OrderBy(plan => Priority(plan.TargetKind))
            .ThenBy(plan => PathLength(plan.Path))
            .FirstOrDefault();
    }

    private static IEnumerable<(Prop Prop, LocalMoveTargetKind Kind)> Candidates(
        IEnumerable<Prop> props,
        Guid creatureId,
        CreatureJobAction action
    ) =>
        action switch
        {
            CreatureJobAction.Sleep => props
                .OfType<Bed>()
                .Where(bed => bed.AssignedCreatureId == creatureId && bed.OccupantId == null)
                .Select(bed => ((Prop)bed, LocalMoveTargetKind.Bed)),
            CreatureJobAction.Work => props
                .OfType<Workstation>()
                .Where(workstation =>
                    workstation.WorkstationType != WorkstationType.Reading
                    && workstation.OccupantId == null
                )
                .Select(workstation => ((Prop)workstation, LocalMoveTargetKind.Workstation))
                .Concat(
                    props
                        .OfType<Seat>()
                        .Where(seat => seat.OccupantId == null)
                        .Select(seat => ((Prop)seat, LocalMoveTargetKind.Seat))
                )
                .OrderBy(candidate => candidate.Item2),
            CreatureJobAction.Idle => props
                .OfType<Seat>()
                .Where(seat => seat.OccupantId == null)
                .Select(seat => ((Prop)seat, LocalMoveTargetKind.Seat)),
            _ => [],
        };

    private static LocalMovePlan? ToPlan(
        Creature creature,
        CreatureJob job,
        Point origin,
        (Prop Prop, LocalMoveTargetKind Kind) candidate,
        Application.Common.Navigation.NavigationGrid grid
    )
    {
        var destination = grid.FindNearestFreePoint(new Point(candidate.Prop.X, candidate.Prop.Y));
        var path = grid.FindReachablePath(origin, destination);
        if (path is null)
        {
            return null;
        }

        return new LocalMovePlan(
            creature.Id,
            job.LocationId,
            job.Id,
            job.Action,
            candidate.Kind,
            candidate.Prop.Id,
            path
        );
    }

    private static double PathLength(IReadOnlyList<Point> path) =>
        path.Zip(path.Skip(1)).Sum(pair => Distance(pair.First, pair.Second));

    private static double Distance(Point first, Point second) =>
        Math.Sqrt(Math.Pow(second.X - first.X, 2) + Math.Pow(second.Y - first.Y, 2));

    private static int Priority(LocalMoveTargetKind kind) =>
        kind switch
        {
            LocalMoveTargetKind.Workstation => 0,
            LocalMoveTargetKind.Bed => 0,
            LocalMoveTargetKind.Seat => 1,
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
}
