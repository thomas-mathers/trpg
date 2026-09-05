using System.Transactions;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Events;
using TRPG.Application.Common.Exceptions;
using TRPG.Application.Common.Queries;
using TRPG.Application.Common.Validation;
using TRPG.Application.Creatures.Queries;
using TRPG.Domain.Models;

namespace TRPG.Application.Creatures.Commands;

public class MovePlayerCommand
{
    [NotEmptyGuid]
    public required Guid PlayerId { get; init; }

    [NotEmptyGuid]
    public required Guid DestinationLocationId { get; init; }

    public required TimeSpan Playtime { get; init; }
}

internal class MovePlayerCommandHandler(
    IDomainEventPublisher<PlayerMovedEvent> domainEvents,
    IQueryHandler<GetCreatureByIdQuery, Creature?> getCreatureById,
    ICommandHandler<UpdateCreaturesCommand> updateCreatures
) : ICommandHandler<MovePlayerCommand>
{
    public async Task Handle(
        MovePlayerCommand command,
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

        await updateCreatures.Handle(
            new UpdateCreaturesCommand
            {
                CreatureIds = [command.PlayerId],
                LocationId = command.DestinationLocationId,
            },
            cancellationToken
        );

        await domainEvents.Publish(
            new PlayerMovedEvent(
                PlayerId: player.Id,
                WorldId: player.WorldId,
                FromLocationId: player.LocationId,
                ToLocationId: command.DestinationLocationId,
                Playtime: command.Playtime
            ),
            cancellationToken
        );

        transaction.Complete();
    }
}
