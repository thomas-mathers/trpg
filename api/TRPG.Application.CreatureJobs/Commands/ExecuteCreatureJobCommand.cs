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
    public required CreaturePosture CurrentPosture { get; init; }
    public required CreatureJobAction CreatureJobAction { get; init; }
    public required Guid JobLocationId { get; init; }
    public Guid? PreferredSeatId { get; init; }
}

internal class ExecuteCreatureJobCommandHandler(
    ICommandHandler<UpdateCreaturesCommand> updateCreatures,
    IQueryHandler<GetBedByLocationIdQuery, Bed?> getBedByLocationId,
    ICommandHandler<SetBedOccupantCommand> setBedOccupant,
    ICommandHandler<TryOccupyAnyAvailableSeatCommand, bool> tryOccupyAnyAvailableSeat,
    ICommandHandler<VacateCreatureSeatCommand> vacateCreatureSeat
) : ICommandHandler<ExecuteCreatureJobCommand>
{
    private static readonly HashSet<CreatureState> NonSchedulableStates = [CreatureState.Dead];

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

        if (command.CurrentState == CreatureState.Sleeping && targetState != CreatureState.Sleeping)
        {
            await ClearBedOccupant(
                command.CurrentLocationId,
                command.CreatureId,
                cancellationToken
            );
        }

        var targetPosture = command.CurrentPosture;
        if (command.CurrentPosture == CreaturePosture.Sitting)
        {
            await vacateCreatureSeat.Handle(
                new VacateCreatureSeatCommand { CreatureId = command.CreatureId },
                cancellationToken
            );
            targetPosture = CreaturePosture.Standing;
        }

        if (command.CreatureJobAction == CreatureJobAction.Idle)
        {
            var seated = await TryOccupyAvailableSeat(command, cancellationToken);
            targetPosture = seated ? CreaturePosture.Sitting : CreaturePosture.Standing;
        }

        if (targetState == CreatureState.Sleeping)
        {
            targetPosture = CreaturePosture.Laying;
        }
        else if (targetPosture == CreaturePosture.Laying)
        {
            targetPosture = CreaturePosture.Standing;
        }

        await updateCreatures.Handle(
            new UpdateCreaturesCommand
            {
                CreatureIds = [command.CreatureId],
                State = targetState,
                Posture = targetPosture,
            },
            cancellationToken
        );

        if (targetState == CreatureState.Sleeping && command.CurrentState != CreatureState.Sleeping)
        {
            await SetBedOccupant(command.JobLocationId, command.CreatureId, cancellationToken);
        }
    }

    private Task<bool> TryOccupyAvailableSeat(
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
