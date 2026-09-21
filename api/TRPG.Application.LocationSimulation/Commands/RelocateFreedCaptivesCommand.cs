using System.Transactions;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Queries;
using TRPG.Application.CreatureJobs;
using TRPG.Application.CreatureJobs.Commands;
using TRPG.Application.CreatureJobs.Queries;
using TRPG.Application.Creatures.Commands;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.Creatures.Results;
using TRPG.Application.Quests.Queries;
using TRPG.Domain;
using TRPG.Domain.Models;

namespace TRPG.Application.LocationSimulation.Commands;

public class RelocateFreedCaptivesCommand
{
    public required Guid WorldId { get; init; }
    public required Guid PlayerId { get; init; }
    public required Guid LocationId { get; init; }
    public required TimeSpan Playtime { get; init; }
}

// A rescued captive already has an ordinary household job schedule (Capture never touches
// CreatureJob rows, only State/LocationId) — it just never runs while nobody catches up their
// actual job location, which the player may never revisit. Resolving it here, the moment the
// player leaves them behind, sends them home immediately instead of leaving them stranded in the
// cell indefinitely. Scoped to FreeCreatureObjective targets specifically (not every idle creature
// at the location) so ordinary captors and ambient dungeon monsters, some of which also have no
// CreatureJob rows, are never mistaken for an abandoned captive.
internal class RelocateFreedCaptivesCommandHandler(
    IQueryHandler<
        GetCreaturesAtLocationQuery,
        IReadOnlyCollection<CreatureResult>
    > getCreaturesAtLocation,
    IQueryHandler<
        GetActiveFreeCreatureObjectiveCreatureIdsQuery,
        IReadOnlySet<Guid>
    > getFreeCreatureObjectiveCreatureIds,
    IQueryHandler<
        GetCreatureJobsByCreatureIdsQuery,
        IReadOnlyDictionary<Guid, IReadOnlyList<CreatureJob>>
    > getJobsByCreatureIds,
    ICommandHandler<ExecuteCreatureJobCommand> executeJob,
    ICommandHandler<DeleteCreaturesCommand> deleteCreatures
) : ICommandHandler<RelocateFreedCaptivesCommand>
{
    public async Task Handle(
        RelocateFreedCaptivesCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var nearby = await getCreaturesAtLocation.Handle(
            new GetCreaturesAtLocationQuery
            {
                WorldId = command.WorldId,
                LocationId = command.LocationId,
            },
            cancellationToken
        );

        var freeCreatureObjectiveCreatureIds = await getFreeCreatureObjectiveCreatureIds.Handle(
            new GetActiveFreeCreatureObjectiveCreatureIdsQuery
            {
                WorldId = command.WorldId,
                PlayerId = command.PlayerId,
            },
            cancellationToken
        );

        var strandedCaptiveIds = nearby
            .Where(creature =>
                creature.State == CreatureState.Idle
                && freeCreatureObjectiveCreatureIds.Contains(creature.Id)
            )
            .Select(creature => creature.Id)
            .ToArray();

        if (strandedCaptiveIds.Length == 0)
        {
            return;
        }

        using var transaction = new TransactionScope(
            TransactionScopeOption.Required,
            TransactionScopeAsyncFlowOption.Enabled
        );

        var jobsByCreatureId = await getJobsByCreatureIds.Handle(
            new GetCreatureJobsByCreatureIdsQuery { CreatureIds = strandedCaptiveIds },
            cancellationToken
        );

        var joblessCaptiveIds = strandedCaptiveIds
            .Where(creatureId =>
                !jobsByCreatureId.TryGetValue(creatureId, out var jobs) || jobs.Count == 0
            )
            .ToArray();
        if (joblessCaptiveIds.Length > 0)
        {
            await deleteCreatures.Handle(
                new DeleteCreaturesCommand { CreatureIds = joblessCaptiveIds },
                cancellationToken
            );
        }

        var currentDate = GameClock.GetCurrentInGameDate(command.Playtime);
        foreach (var creatureId in strandedCaptiveIds.Except(joblessCaptiveIds))
        {
            var dueJob = CreatureJobScheduling.FindDueJob(
                jobsByCreatureId[creatureId],
                currentDate.Weekday,
                currentDate.Hour
            );
            if (dueJob == null)
            {
                continue;
            }

            await executeJob.Handle(
                new ExecuteCreatureJobCommand
                {
                    CreatureId = creatureId,
                    CurrentLocationId = command.LocationId,
                    CurrentState = CreatureState.Idle,
                    CreatureJobAction = dueJob.Action,
                    JobLocationId = dueJob.LocationId,
                },
                cancellationToken
            );
        }

        transaction.Complete();
    }
}
