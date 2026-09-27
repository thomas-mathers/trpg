using System.Transactions;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Queries;
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
    public required GameInstant GameTime { get; init; }
}

// Quest-target filtering keeps unrelated idle dungeon creatures out of captive cleanup.
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
    ICommandHandler<
        SyncCreatureJobSchedulesCommand,
        SyncCreatureJobSchedulesResult
    > syncCreatureJobSchedules,
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

        await syncCreatureJobSchedules.Handle(
            new SyncCreatureJobSchedulesCommand
            {
                CreatureIds = strandedCaptiveIds.Except(joblessCaptiveIds).ToArray(),
                GameTime = command.GameTime,
                BecameAvailableAtGameTimeByCreatureId = strandedCaptiveIds
                    .Except(joblessCaptiveIds)
                    .ToDictionary(creatureId => creatureId, _ => command.GameTime),
            },
            cancellationToken
        );

        transaction.Complete();
    }
}
