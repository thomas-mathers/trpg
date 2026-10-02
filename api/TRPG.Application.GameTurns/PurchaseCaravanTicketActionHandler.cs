using TRPG.Application.Caravans.Commands;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Exceptions;
using TRPG.Application.Common.Queries;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.GameSessions.Queries;
using TRPG.Domain;
using TRPG.Domain.Models;

namespace TRPG.Application.GameTurns;

internal class PurchaseCaravanTicketActionHandler(
    GameActionRunner actionRunner,
    IQueryHandler<GetCreatureByIdQuery, Creature?> getCreatureById,
    IQueryHandler<GetGameTimeQuery, GameInstant> getGameTime,
    ICommandHandler<PurchaseCaravanTicketCommand, PurchaseCaravanTicketResult> purchaseCaravanTicket
)
{
    public Task<ActionOutcome> Handle(
        GameTurnSession session,
        Guid caravanId,
        Guid destinationLocationId,
        CancellationToken cancellationToken = default
    ) =>
        actionRunner.Run(
            session,
            ct => Resolve(session, caravanId, destinationLocationId, ct),
            cancellationToken
        );

    private async Task<ActionOutcome> Resolve(
        GameTurnSession session,
        Guid caravanId,
        Guid destinationLocationId,
        CancellationToken cancellationToken
    )
    {
        var player =
            await getCreatureById.Handle(
                new GetCreatureByIdQuery { Id = session.PlayerId },
                cancellationToken
            ) ?? throw new EntityNotFoundException(nameof(Creature), session.PlayerId);
        var gameTime = await getGameTime.Handle(
            new GetGameTimeQuery { SessionId = session.SessionId },
            cancellationToken
        );

        var result = await purchaseCaravanTicket.Handle(
            new PurchaseCaravanTicketCommand
            {
                PlayerId = session.PlayerId,
                WorldId = session.WorldId,
                CaravanId = caravanId,
                DestinationLocationId = destinationLocationId,
                PlayerLocationId = player.LocationId,
                GameTime = gameTime,
            },
            cancellationToken
        );

        return result.Outcome switch
        {
            PurchaseCaravanTicketOutcome.Purchased => ActionOutcome.Success,
            PurchaseCaravanTicketOutcome.InsufficientGold => ActionOutcome.Failed(
                ActionFailure.InsufficientGold
            ),
            PurchaseCaravanTicketOutcome.AlreadyHoldsTicket => ActionOutcome.Failed(
                ActionFailure.AlreadyHoldsTicket
            ),
            PurchaseCaravanTicketOutcome.InvalidDestination => ActionOutcome.Failed(
                ActionFailure.InvalidDestination
            ),
            PurchaseCaravanTicketOutcome.TravelSuspended => ActionOutcome.Failed(
                ActionFailure.TravelSuspended
            ),
            _ => ActionOutcome.Failed(ActionFailure.CaravanNotPresent),
        };
    }
}
