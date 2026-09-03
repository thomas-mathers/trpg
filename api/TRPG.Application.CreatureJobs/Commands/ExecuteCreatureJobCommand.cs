using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Queries;
using TRPG.Application.Creatures.Commands;
using TRPG.Application.Props.Commands;
using TRPG.Application.Props.Queries;
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
    ICommandHandler<UpdateCreaturesCommand> updateCreatures,
    IQueryHandler<GetBedByLocationIdQuery, Bed?> getBedByLocationId,
    ICommandHandler<SetBedOccupantCommand> setBedOccupant
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

        if (command.CurrentState == CreatureState.Sleeping && targetState != CreatureState.Sleeping)
        {
            await ClearBedOccupant(
                command.CurrentLocationId,
                command.CreatureId,
                cancellationToken
            );
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

        if (targetState == CreatureState.Sleeping && command.CurrentState != CreatureState.Sleeping)
        {
            await SetBedOccupant(command.JobLocationId, command.CreatureId, cancellationToken);
        }
    }

    private async Task SetBedOccupant(
        Guid locationId,
        Guid creatureId,
        CancellationToken cancellationToken
    )
    {
        var bed = await getBedByLocationId.Handle(
            new GetBedByLocationIdQuery { LocationId = locationId },
            cancellationToken
        );
        if (bed?.AssignedCreatureId == creatureId)
        {
            await setBedOccupant.Handle(
                new SetBedOccupantCommand { BedId = bed.Id, OccupantId = creatureId },
                cancellationToken
            );
        }
    }

    private async Task ClearBedOccupant(
        Guid locationId,
        Guid creatureId,
        CancellationToken cancellationToken
    )
    {
        var bed = await getBedByLocationId.Handle(
            new GetBedByLocationIdQuery { LocationId = locationId },
            cancellationToken
        );
        if (bed?.OccupantId == creatureId)
        {
            await setBedOccupant.Handle(
                new SetBedOccupantCommand { BedId = bed.Id, OccupantId = null },
                cancellationToken
            );
        }
    }
}
