using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Queries;
using TRPG.Application.CreatureJobs.Queries;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.GameSessions.Queries;
using TRPG.Domain;
using TRPG.Domain.Models;

namespace TRPG.Application.CreatureJobs.Commands;

public class SyncCreatureToCurrentJobCommand
{
    public required Guid WorldId { get; init; }
    public required Guid CreatureId { get; init; }
}

// A creature released from something that suppressed its schedule (captivity, for instance) does
// not resume its routine until something happens to revisit one of its own job locations. This
// jumps it straight to wherever its own existing schedule already says it belongs right now.
internal class SyncCreatureToCurrentJobCommandHandler(
    IQueryHandler<GetCreatureByIdQuery, Creature?> getCreatureById,
    IQueryHandler<GetPlaytimeByWorldIdQuery, TimeSpan> getPlaytimeByWorldId,
    IQueryHandler<
        GetCreatureJobsByCreatureIdsQuery,
        IReadOnlyDictionary<Guid, IReadOnlyList<CreatureJob>>
    > getJobsByCreatureIds,
    ICommandHandler<ExecuteCreatureJobCommand> executeJob
) : ICommandHandler<SyncCreatureToCurrentJobCommand>
{
    public async Task Handle(
        SyncCreatureToCurrentJobCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var creature = await getCreatureById.Handle(
            new GetCreatureByIdQuery { Id = command.CreatureId },
            cancellationToken
        );
        if (creature == null)
        {
            return;
        }

        var playtime = await getPlaytimeByWorldId.Handle(
            new GetPlaytimeByWorldIdQuery { WorldId = command.WorldId },
            cancellationToken
        );
        var currentDate = GameClock.GetCurrentInGameDate(playtime);

        var jobsByCreatureId = await getJobsByCreatureIds.Handle(
            new GetCreatureJobsByCreatureIdsQuery { CreatureIds = [command.CreatureId] },
            cancellationToken
        );
        if (!jobsByCreatureId.TryGetValue(command.CreatureId, out var jobs))
        {
            return;
        }

        var dueJob = CreatureJobScheduling.FindDueJob(jobs, currentDate.Weekday, currentDate.Hour);
        if (dueJob == null)
        {
            return;
        }

        await executeJob.Handle(
            new ExecuteCreatureJobCommand
            {
                CreatureId = creature.Id,
                CurrentLocationId = creature.LocationId,
                CurrentState = creature.State,
                CreatureJobAction = dueJob.Action,
                JobLocationId = dueJob.LocationId,
            },
            cancellationToken
        );
    }
}
