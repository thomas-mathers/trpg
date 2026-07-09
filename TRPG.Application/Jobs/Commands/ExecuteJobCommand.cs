using TRPG.Application.Creatures.Commands;
using TRPG.Data.Models;

namespace TRPG.Application.Jobs.Commands;

internal class ExecuteJobCommand
{
    public required Creature Creature { get; init; }
    public required Job Job { get; init; }
}

internal class ExecuteJobCommandHandler(UpdateCreatureCommandHandler updateCreature)
{
    public async Task Handle(
        ExecuteJobCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var targetState = command.Job.Action switch
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
                command.Job.Action,
                "Unhandled JobAction."
            ),
        };

        if (targetState == null)
        {
            return;
        }

        var creature = command.Creature;
        if (creature.RoomId == command.Job.RoomId && creature.State == targetState)
        {
            return;
        }

        creature.RoomId = command.Job.RoomId;
        creature.State = targetState.Value;
        await updateCreature.Handle(
            new UpdateCreatureCommand { Creature = creature },
            cancellationToken
        );
    }
}
