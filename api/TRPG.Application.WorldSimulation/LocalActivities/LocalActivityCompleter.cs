using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Queries;
using TRPG.Application.CreatureJobs.Commands;
using TRPG.Application.Creatures.Commands;
using TRPG.Application.Props.Commands;
using TRPG.Application.Props.Queries;
using TRPG.Domain.Models;

namespace TRPG.Application.WorldSimulation.LocalActivities;

internal sealed class LocalActivityCompleter(
    ICommandHandler<TryOccupyWorkstationCommand, bool> tryOccupyWorkstation,
    ICommandHandler<TryOccupySeatCommand, bool> tryOccupySeat,
    ICommandHandler<TryOccupyAssignedBedCommand, bool> tryOccupyAssignedBed,
    ICommandHandler<ExecuteCreatureJobCommand> executeCreatureJob,
    ICommandHandler<SetCreatureActivityCommand> setCreatureActivity,
    ICommandHandler<TryStartSittingCommand, bool> tryStartSitting,
    IQueryHandler<GetPropByIdQuery, Prop?> getPropById
)
{
    public async Task<bool> CompleteAtTarget(
        Creature creature,
        LocalMovePlan move,
        CancellationToken cancellationToken
    )
    {
        var claimed = await TryClaim(creature.Id, move, cancellationToken);
        if (!claimed)
        {
            return false;
        }

        if (move.Action == CreatureJobAction.Idle)
        {
            await setCreatureActivity.Handle(
                new SetCreatureActivityCommand { CreatureIds = [creature.Id], Activity = null },
                cancellationToken
            );
        }
        else
        {
            await ExecuteJob(creature, move, cancellationToken);
        }

        if (move.TargetKind == LocalMoveTargetKind.Seat)
        {
            await Sit(creature.Id, move.TargetPropId, cancellationToken);
        }
        return true;
    }

    private Task<bool> TryClaim(
        Guid creatureId,
        LocalMovePlan move,
        CancellationToken cancellationToken
    ) =>
        move.TargetKind switch
        {
            LocalMoveTargetKind.Workstation => tryOccupyWorkstation.Handle(
                new TryOccupyWorkstationCommand
                {
                    WorkstationId = move.TargetPropId,
                    CreatureId = creatureId,
                },
                cancellationToken
            ),
            LocalMoveTargetKind.Seat => tryOccupySeat.Handle(
                new TryOccupySeatCommand { SeatId = move.TargetPropId, CreatureId = creatureId },
                cancellationToken
            ),
            LocalMoveTargetKind.Bed => tryOccupyAssignedBed.Handle(
                new TryOccupyAssignedBedCommand
                {
                    BedId = move.TargetPropId,
                    CreatureId = creatureId,
                },
                cancellationToken
            ),
            _ => throw new ArgumentOutOfRangeException(nameof(move)),
        };

    public async Task CompleteStanding(
        Creature creature,
        CreatureJob job,
        CancellationToken cancellationToken
    )
    {
        if (job.Action == CreatureJobAction.Idle)
        {
            await setCreatureActivity.Handle(
                new SetCreatureActivityCommand { CreatureIds = [creature.Id], Activity = null },
                cancellationToken
            );
            return;
        }

        await executeCreatureJob.Handle(ToJobCommand(creature, job), cancellationToken);
    }

    private Task ExecuteJob(
        Creature creature,
        LocalMovePlan move,
        CancellationToken cancellationToken
    ) =>
        executeCreatureJob.Handle(
            new ExecuteCreatureJobCommand
            {
                CreatureId = creature.Id,
                CurrentLocationId = creature.LocationId,
                CurrentCondition = creature.Condition,
                CurrentPosture = creature.Posture,
                CreatureJobAction = move.Action,
                JobLocationId = move.LocationId,
            },
            cancellationToken
        );

    private async Task Sit(Guid creatureId, Guid seatId, CancellationToken cancellationToken)
    {
        if (
            await getPropById.Handle(new GetPropByIdQuery { Id = seatId }, cancellationToken)
            is not Seat seat
        )
        {
            return;
        }

        await tryStartSitting.Handle(
            new TryStartSittingCommand
            {
                CreatureId = creatureId,
                LocationId = seat.LocationId,
                X = seat.X,
                Y = seat.Y,
                Angle = seat.Angle,
            },
            cancellationToken
        );
    }

    private static ExecuteCreatureJobCommand ToJobCommand(Creature creature, CreatureJob job) =>
        new()
        {
            CreatureId = creature.Id,
            CurrentLocationId = creature.LocationId,
            CurrentCondition = creature.Condition,
            CurrentPosture = creature.Posture,
            CreatureJobAction = job.Action,
            JobLocationId = job.LocationId,
        };
}
