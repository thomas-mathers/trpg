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
    ICommandHandler<SetBedOccupantCommand> setBedOccupant,
    IQueryHandler<
        GetAvailableSeatsByLocationIdQuery,
        IReadOnlyList<Seat>
    > getAvailableSeatsByLocationId,
    ICommandHandler<TryOccupySeatCommand, bool> tryOccupySeat,
    ICommandHandler<VacateCreatureSeatCommand> vacateCreatureSeat
) : ICommandHandler<ExecuteCreatureJobCommand>
{
    // These states temporarily suppress ordinary schedule effects.
    private static readonly HashSet<CreatureState> NonSchedulableStates =
    [
        CreatureState.Alerted,
        CreatureState.Dead,
        CreatureState.Restrained,
    ];

    public async Task Handle(
        ExecuteCreatureJobCommand command,
        CancellationToken cancellationToken = default
    )
    {
        if (NonSchedulableStates.Contains(command.CurrentState))
        {
            return;
        }

        if (command.CurrentLocationId != command.JobLocationId)
        {
            return;
        }

        if (
            command.CreatureJobAction == CreatureJobAction.Idle
            && command.CurrentState == CreatureState.Sitting
        )
        {
            return;
        }

        var targetState = command.CreatureJobAction switch
        {
            CreatureJobAction.Sleep => CreatureState.Sleeping,
            CreatureJobAction.Work => CreatureState.Working,
            CreatureJobAction.Idle => CreatureState.Idle,
            CreatureJobAction.Study => CreatureState.Studying,
            CreatureJobAction.Pray => CreatureState.Praying,
            CreatureJobAction.Eat => CreatureState.Eating,
            _ => throw new ArgumentOutOfRangeException(
                nameof(command),
                command.CreatureJobAction,
                "Unhandled CreatureJobAction."
            ),
        };

        if (
            command.CurrentLocationId == command.JobLocationId
            && command.CurrentState == targetState
            && command.CreatureJobAction != CreatureJobAction.Idle
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

        if (command.CurrentState == CreatureState.Sitting)
        {
            await vacateCreatureSeat.Handle(
                new VacateCreatureSeatCommand { CreatureId = command.CreatureId },
                cancellationToken
            );
        }

        if (command.CreatureJobAction == CreatureJobAction.Idle)
        {
            var seated = await TryOccupyAvailableSeat(command, cancellationToken);
            targetState = seated ? CreatureState.Sitting : CreatureState.Idle;
        }

        await updateCreatures.Handle(
            new UpdateCreaturesCommand { CreatureIds = [command.CreatureId], State = targetState },
            cancellationToken
        );

        if (targetState == CreatureState.Sleeping && command.CurrentState != CreatureState.Sleeping)
        {
            await SetBedOccupant(command.JobLocationId, command.CreatureId, cancellationToken);
        }
    }

    private async Task<bool> TryOccupyAvailableSeat(
        ExecuteCreatureJobCommand command,
        CancellationToken cancellationToken
    )
    {
        var seats = await getAvailableSeatsByLocationId.Handle(
            new GetAvailableSeatsByLocationIdQuery { LocationId = command.JobLocationId },
            cancellationToken
        );
        foreach (var seat in seats)
        {
            var occupied = await tryOccupySeat.Handle(
                new TryOccupySeatCommand { SeatId = seat.Id, CreatureId = command.CreatureId },
                cancellationToken
            );
            if (occupied)
            {
                return true;
            }
        }

        return false;
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
