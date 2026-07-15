using TRPG.Application.Common;
using TRPG.Application.Creatures.Commands;
using TRPG.Data.Models;

namespace TRPG.Application.Jobs.Commands;

internal class ExecuteJobCommand
{
    public required Guid CreatureId { get; init; }
    public required Guid? CurrentRoomId { get; init; }
    public required CreatureState CurrentState { get; init; }
    public required JobAction JobAction { get; init; }
    public required Guid? JobRoomId { get; init; }
}

internal class ExecuteJobCommandHandler(UpdateCreaturesCommandHandler updateCreature)
{
    public async Task Handle(
        ExecuteJobCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var targetState = command.JobAction switch
        {
            JobAction.Sleep => CreatureState.Sleeping,
            JobAction.Work => CreatureState.Busy,
            JobAction.Idle => CreatureState.Idle,
            JobAction.Study => CreatureState.Studying,
            JobAction.Pray => CreatureState.Praying,
            JobAction.Train => CreatureState.Training,
            JobAction.Sit => CreatureState.Sitting,
            JobAction.Patrol or JobAction.Socialize => (CreatureState?)null,
            _ => throw new ArgumentOutOfRangeException(
                nameof(command),
                command.JobAction,
                "Unhandled JobAction."
            ),
        };

        if (targetState == null)
        {
            return;
        }

        if (command.CurrentRoomId == command.JobRoomId && command.CurrentState == targetState)
        {
            return;
        }

        await updateCreature.Handle(
            new UpdateCreaturesCommand
            {
                CreatureIds = [command.CreatureId],
                RoomId = Optional<Guid?>.Of(command.JobRoomId),
                State = targetState.Value,
            },
            cancellationToken
        );
    }
}
