using TRPG.Application.Common.Commands;
using TRPG.Application.Creatures.Commands;
using TRPG.Domain.Models;

namespace TRPG.Application.CreatureJobs.Commands;

public class ExecuteCreatureJobCommand
{
    public required Guid CreatureId { get; init; }
    public required Guid CurrentLocationId { get; init; }
    public required CreatureState CurrentState { get; init; }
    public required CreatureJobAction CreatureJobAction { get; init; }
    public required Guid JobLocationId { get; init; }
}

internal class ExecuteCreatureJobCommandHandler(
    ICommandHandler<UpdateCreaturesCommand> updateCreatures
) : ICommandHandler<ExecuteCreatureJobCommand>
{
    public async Task Handle(
        ExecuteCreatureJobCommand command,
        CancellationToken cancellationToken = default
    )
    {
        if (command.CurrentState is CreatureState.Alerted or CreatureState.Dead)
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
            _ => throw new ArgumentOutOfRangeException(
                nameof(command),
                command.CreatureJobAction,
                "Unhandled CreatureJobAction."
            ),
        };

        if (
            command.CurrentLocationId == command.JobLocationId
            && command.CurrentState == targetState
        )
        {
            return;
        }

        await updateCreatures.Handle(
            new UpdateCreaturesCommand
            {
                CreatureIds = [command.CreatureId],
                LocationId = command.JobLocationId,
                State = targetState,
            },
            cancellationToken
        );
    }
}
