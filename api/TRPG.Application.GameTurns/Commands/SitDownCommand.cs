using System.Transactions;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Exceptions;
using TRPG.Application.Common.Queries;
using TRPG.Application.Creatures.Commands;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.Props.Commands;
using TRPG.Application.Props.Queries;
using TRPG.Domain.Models;

namespace TRPG.Application.GameTurns.Commands;

public class SitDownCommand
{
    public required Guid PlayerId { get; init; }
    public required Guid SeatId { get; init; }
}

public enum SitDownResult
{
    Success,
    SeatOccupied,
    SeatNotNearby,
    PlayerNotIdle,
}

internal class SitDownCommandHandler(
    IQueryHandler<GetCreatureByIdQuery, Creature?> getCreatureById,
    IQueryHandler<GetPropByIdQuery, Prop?> getPropById,
    ICommandHandler<TryOccupySeatCommand, bool> tryOccupySeat,
    ICommandHandler<TryStartSittingCommand, bool> tryStartSitting
) : ICommandHandler<SitDownCommand, SitDownResult>
{
    public async Task<SitDownResult> Handle(
        SitDownCommand command,
        CancellationToken cancellationToken = default
    )
    {
        using var transaction = new TransactionScope(
            TransactionScopeOption.Required,
            TransactionScopeAsyncFlowOption.Enabled
        );

        var player =
            await getCreatureById.Handle(
                new GetCreatureByIdQuery { Id = command.PlayerId },
                cancellationToken
            ) ?? throw new EntityNotFoundException(nameof(Creature), command.PlayerId);
        var prop =
            await getPropById.Handle(
                new GetPropByIdQuery { Id = command.SeatId },
                cancellationToken
            ) ?? throw new EntityNotFoundException(nameof(Prop), command.SeatId);

        if (prop is not Seat || prop.LocationId != player.LocationId)
        {
            return SitDownResult.SeatNotNearby;
        }

        if (player.State != CreatureState.Idle)
        {
            return SitDownResult.PlayerNotIdle;
        }

        var occupied = await tryOccupySeat.Handle(
            new TryOccupySeatCommand { SeatId = command.SeatId, CreatureId = command.PlayerId },
            cancellationToken
        );
        if (!occupied)
        {
            return SitDownResult.SeatOccupied;
        }

        var sitting = await tryStartSitting.Handle(
            new TryStartSittingCommand
            {
                CreatureId = command.PlayerId,
                LocationId = player.LocationId,
            },
            cancellationToken
        );
        if (!sitting)
        {
            return SitDownResult.PlayerNotIdle;
        }

        transaction.Complete();
        return SitDownResult.Success;
    }
}
