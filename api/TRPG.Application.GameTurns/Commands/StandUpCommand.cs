using System.Transactions;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Exceptions;
using TRPG.Application.Common.Queries;
using TRPG.Application.Creatures.Commands;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.Props.Commands;
using TRPG.Domain.Models;

namespace TRPG.Application.GameTurns.Commands;

public class StandUpCommand
{
    public required Guid PlayerId { get; init; }
}

public enum StandUpResult
{
    Success,
    PlayerNotSitting,
}

internal class StandUpCommandHandler(
    IQueryHandler<GetCreatureByIdQuery, Creature?> getCreatureById,
    ICommandHandler<VacateCreatureSeatCommand> vacateCreatureSeat,
    ICommandHandler<StopSittingCommand> stopSitting
) : ICommandHandler<StandUpCommand, StandUpResult>
{
    public async Task<StandUpResult> Handle(
        StandUpCommand command,
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
        if (player.State != CreatureState.Sitting)
        {
            return StandUpResult.PlayerNotSitting;
        }

        await vacateCreatureSeat.Handle(
            new VacateCreatureSeatCommand { CreatureId = command.PlayerId },
            cancellationToken
        );
        await stopSitting.Handle(
            new StopSittingCommand { CreatureId = command.PlayerId },
            cancellationToken
        );

        transaction.Complete();
        return StandUpResult.Success;
    }
}
