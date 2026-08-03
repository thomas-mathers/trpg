using TRPG.Application.Common;
using TRPG.Application.Creatures.Commands;
using TRPG.Data.Models;

namespace TRPG.Application.CreatureJobs.Commands;

internal class ExecuteCreatureJobCommand
{
    public required Guid CreatureId { get; init; }
    public required Guid? CurrentRoomId { get; init; }
    public required CreatureState CurrentState { get; init; }
    public required CreatureJobAction CreatureJobAction { get; init; }
    public required Guid? JobRoomId { get; init; }
    public required Guid? JobDistrictId { get; init; }
}

internal class ExecuteCreatureJobCommandHandler(UpdateCreaturesCommandHandler updateCreature)
{
    public async Task Handle(
        ExecuteCreatureJobCommand command,
        CancellationToken cancellationToken = default
    )
    {
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

        if (command.CurrentRoomId == command.JobRoomId && command.CurrentState == targetState)
        {
            return;
        }

        await updateCreature.Handle(
            new UpdateCreaturesCommand
            {
                CreatureIds = [command.CreatureId],
                RoomId = Optional<Guid?>.Of(command.JobRoomId),
                DistrictId = Optional<Guid?>.Of(command.JobDistrictId),
                State = targetState.Value,
            },
            cancellationToken
        );
    }
}
