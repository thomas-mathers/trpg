using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Queries;
using TRPG.Application.CreatureJobs.Queries;
using TRPG.Application.Creatures.Commands;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.Creatures.Results;
using TRPG.Application.Quests.Queries;
using TRPG.Domain.Models;

namespace TRPG.Application.WorldSimulation.Commands;

public class RemoveJoblessFreedCaptivesCommand
{
    public required Guid WorldId { get; init; }
    public required Guid PlayerId { get; init; }
    public required Guid LocationId { get; init; }
}

// Quest-target filtering keeps unrelated idle dungeon creatures out of captive cleanup.
internal class RemoveJoblessFreedCaptivesCommandHandler(
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
    ICommandHandler<DeleteCreaturesCommand> deleteCreatures
) : ICommandHandler<RemoveJoblessFreedCaptivesCommand>
{
    public async Task Handle(
        RemoveJoblessFreedCaptivesCommand command,
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
                !creature.IsRestrained && freeCreatureObjectiveCreatureIds.Contains(creature.Id)
            )
            .Select(creature => creature.Id)
            .ToArray();
        if (strandedCaptiveIds.Length == 0)
        {
            return;
        }

        var jobsByCreatureId = await getJobsByCreatureIds.Handle(
            new GetCreatureJobsByCreatureIdsQuery { CreatureIds = strandedCaptiveIds },
            cancellationToken
        );
        var joblessCaptiveIds = strandedCaptiveIds
            .Where(creatureId =>
                !jobsByCreatureId.TryGetValue(creatureId, out var jobs) || jobs.Count == 0
            )
            .ToArray();
        if (joblessCaptiveIds.Length == 0)
        {
            return;
        }

        await deleteCreatures.Handle(
            new DeleteCreaturesCommand { CreatureIds = joblessCaptiveIds },
            cancellationToken
        );
    }
}
