using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Handling;
using TRPG.Data;
using TRPG.Data.Models;

namespace TRPG.Application.CreatureJobs.Commands;

public class ExecuteCreatureJobCommand
{
    public required Guid CreatureId { get; init; }
    public required Guid CurrentLocationId { get; init; }
    public required CreatureState CurrentState { get; init; }
    public required CreatureJobAction CreatureJobAction { get; init; }
    public required Guid JobLocationId { get; init; }
}

public class ExecuteCreatureJobCommandHandler(TrpgDbContext context)
    : ICommandHandler<ExecuteCreatureJobCommand>
{
    public async Task Handle(
        ExecuteCreatureJobCommand command,
        CancellationToken cancellationToken = default
    )
    {
        if (command.CurrentState == CreatureState.Alerted)
        {
            return;
        }

        var targetState = command.CreatureJobAction switch
        {
            CreatureJobAction.Sleep => CreatureState.Sleeping,
            CreatureJobAction.Work => CreatureState.Busy,
            CreatureJobAction.Idle => CreatureState.Idle,
            CreatureJobAction.Study => CreatureState.Studying,
            CreatureJobAction.Pray => CreatureState.Praying,
            CreatureJobAction.Train => CreatureState.Training,
            CreatureJobAction.Sit => CreatureState.Sitting,
            CreatureJobAction.Patrol or CreatureJobAction.Socialize => (CreatureState?)null,
            _ => throw new ArgumentOutOfRangeException(
                nameof(command),
                command.CreatureJobAction,
                "Unhandled CreatureJobAction."
            ),
        };

        if (targetState == null)
        {
            return;
        }

        if (
            command.CurrentLocationId == command.JobLocationId
            && command.CurrentState == targetState
        )
        {
            return;
        }

        await context
            .Creatures.Where(c => c.Id == command.CreatureId)
            .ExecuteUpdateAsync(
                s =>
                    s.SetProperty(c => c.LocationId, command.JobLocationId)
                        .SetProperty(c => c.State, targetState.Value),
                cancellationToken
            );
    }
}
