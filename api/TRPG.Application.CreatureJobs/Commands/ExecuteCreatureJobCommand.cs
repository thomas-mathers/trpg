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
    public required CreatureCondition CurrentCondition { get; init; }
    public required CreaturePosture CurrentPosture { get; init; }
    public required CreatureJobAction CreatureJobAction { get; init; }
    public required Guid JobLocationId { get; init; }
    public Guid? PreferredSeatId { get; init; }
}

internal class ExecuteCreatureJobCommandHandler(
    ICommandHandler<PutCreaturesToSleepCommand> putCreaturesToSleep,
    ICommandHandler<SetCreatureActivityCommand> setCreatureActivity,
    ICommandHandler<TryStartSittingCommand, bool> tryStartSitting,
    ICommandHandler<StopSittingCommand> stopSitting,
    IQueryHandler<GetBedByLocationIdQuery, Bed?> getBedByLocationId,
    ICommandHandler<SetBedOccupantCommand> setBedOccupant,
    ICommandHandler<TryOccupyAnyAvailableSeatCommand, Placement?> tryOccupyAnyAvailableSeat,
    ICommandHandler<VacateCreatureSeatCommand> vacateCreatureSeat
) : ICommandHandler<ExecuteCreatureJobCommand>
{
    public async Task Handle(
        ExecuteCreatureJobCommand command,
        CancellationToken cancellationToken = default
    )
    {
        if (command.CurrentCondition == CreatureCondition.Dead)
        {
            return;
        }

        if (command.CurrentLocationId != command.JobLocationId)
        {
            return;
        }

        if (command.CreatureJobAction == CreatureJobAction.Sleep)
        {
            await Sleep(command, cancellationToken);
            return;
        }

        await StayAwake(command, cancellationToken);
    }

    private async Task Sleep(ExecuteCreatureJobCommand command, CancellationToken cancellationToken)
    {
        await VacateSeatIfSitting(command, cancellationToken);
        await putCreaturesToSleep.Handle(
            new PutCreaturesToSleepCommand { CreatureIds = [command.CreatureId] },
            cancellationToken
        );

        if (command.CurrentCondition != CreatureCondition.Sleeping)
        {
            await SetBedOccupant(command.JobLocationId, command.CreatureId, cancellationToken);
        }
    }

    private async Task StayAwake(
        ExecuteCreatureJobCommand command,
        CancellationToken cancellationToken
    )
    {
        if (command.CurrentCondition == CreatureCondition.Sleeping)
        {
            await ClearBedOccupant(
                command.CurrentLocationId,
                command.CreatureId,
                cancellationToken
            );
        }

        await VacateSeatIfSitting(command, cancellationToken);
        var activity = command.CreatureJobAction.ToActivity();
        await setCreatureActivity.Handle(
            new SetCreatureActivityCommand
            {
                CreatureIds = [command.CreatureId],
                Activity = activity,
            },
            cancellationToken
        );

        if (command.CreatureJobAction == CreatureJobAction.Idle)
        {
            await SitIfSeatAvailable(command, cancellationToken);
        }
    }

    private async Task VacateSeatIfSitting(
        ExecuteCreatureJobCommand command,
        CancellationToken cancellationToken
    )
    {
        if (command.CurrentPosture != CreaturePosture.Sitting)
        {
            return;
        }

        await vacateCreatureSeat.Handle(
            new VacateCreatureSeatCommand { CreatureId = command.CreatureId },
            cancellationToken
        );
        await stopSitting.Handle(
            new StopSittingCommand { CreatureId = command.CreatureId },
            cancellationToken
        );
    }

    private async Task SitIfSeatAvailable(
        ExecuteCreatureJobCommand command,
        CancellationToken cancellationToken
    )
    {
        var seat = await TryOccupyAvailableSeat(command, cancellationToken);
        if (seat != null)
        {
            await tryStartSitting.Handle(
                new TryStartSittingCommand
                {
                    CreatureId = command.CreatureId,
                    LocationId = command.JobLocationId,
                    X = seat.X,
                    Y = seat.Y,
                    Angle = seat.Angle,
                },
                cancellationToken
            );
        }
    }

    private Task<Placement?> TryOccupyAvailableSeat(
        ExecuteCreatureJobCommand command,
        CancellationToken cancellationToken
    ) =>
        tryOccupyAnyAvailableSeat.Handle(
            new TryOccupyAnyAvailableSeatCommand
            {
                LocationId = command.JobLocationId,
                CreatureId = command.CreatureId,
                PreferredSeatId = command.PreferredSeatId,
            },
            cancellationToken
        );

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
