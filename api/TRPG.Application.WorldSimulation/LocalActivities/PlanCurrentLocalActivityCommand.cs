using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Commands;
using TRPG.Application.CreatureJobs;
using TRPG.Data.ModuleContexts;
using TRPG.Domain;
using TRPG.Domain.Models;

namespace TRPG.Application.WorldSimulation.LocalActivities;

public class PlanCurrentLocalActivityCommand
{
    public required Guid CreatureId { get; init; }
    public required GameInstant At { get; init; }
}

internal sealed class PlanCurrentLocalActivityCommandHandler(
    ICreaturesDbContext creatures,
    ICreatureJobsDbContext jobs,
    LocalActivityPlanner planner,
    LocalActivityCompleter completer
) : ICommandHandler<PlanCurrentLocalActivityCommand, LocalActivityPlanResult>
{
    public async Task<LocalActivityPlanResult> Handle(
        PlanCurrentLocalActivityCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var creature = await creatures.Creatures.FindAsync([command.CreatureId], cancellationToken);
        if (creature is null)
        {
            return new LocalActivityPlanResult(false);
        }

        var creatureJobs = await jobs
            .CreatureJobs.AsNoTracking()
            .Where(job => job.CreatureId == creature.Id)
            .OrderByDescending(job => job.Priority)
            .ToArrayAsync(cancellationToken);
        var job = CreatureJobScheduling.FindDueJob(
            creatureJobs,
            command.At.Value.DayOfWeek,
            command.At.Value.Hour
        );
        if (job is null || job.LocationId != creature.LocationId)
        {
            return new LocalActivityPlanResult(false);
        }
        if (await planner.IsSettled(creature, job, cancellationToken))
        {
            return new LocalActivityPlanResult(true);
        }

        var move = await planner.Plan(
            creature,
            job,
            new Point(creature.X, creature.Y),
            cancellationToken
        );
        if (move is not null)
        {
            return new LocalActivityPlanResult(true, move);
        }

        await completer.CompleteStanding(creature, job, cancellationToken);
        return new LocalActivityPlanResult(true);
    }
}
