using TRPG.Application.Caravans.Commands;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Exceptions;
using TRPG.Application.Common.Queries;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.GameSessions.Queries;
using TRPG.Domain.Models;

namespace TRPG.Application.GameTurns;

internal class StreamPurchaseCaravanTicketTurnHandler(
    GameTurnStreamer streamer,
    IQueryHandler<GetCreatureByIdQuery, Creature?> getCreatureById,
    IQueryHandler<GetPlaytimeQuery, TimeSpan> getPlaytime,
    ICommandHandler<PurchaseCaravanTicketCommand, PurchaseCaravanTicketResult> purchaseCaravanTicket
)
{
    public IAsyncEnumerable<string> Handle(
        GameTurnSession session,
        Guid caravanId,
        Guid destinationLocationId,
        CancellationToken cancellationToken = default
    ) =>
        streamer.StreamTurn(
            session,
            ct => ResolveTurn(session, caravanId, destinationLocationId, ct),
            cancellationToken
        );

    private async Task<GameTurnPrompt> ResolveTurn(
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
        var playtime = await getPlaytime.Handle(
            new GetPlaytimeQuery { SessionId = session.SessionId },
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
                Playtime = playtime,
            },
            cancellationToken
        );

        return result.Outcome switch
        {
            PurchaseCaravanTicketOutcome.Purchased => new GameTurnPrompt.Narrate(
                $"The player just paid {result.GoldCharged} gold for a caravan ticket. Narrate a "
                    + "brief, in-character reaction from the caravan driver handing over the "
                    + "ticket, in one or two sentences."
            ),
            PurchaseCaravanTicketOutcome.InsufficientGold => new GameTurnPrompt.Reply(
                "You don't have enough gold for a ticket."
            ),
            PurchaseCaravanTicketOutcome.AlreadyHoldsTicket => new GameTurnPrompt.Reply(
                "You already hold a ticket for this caravan."
            ),
            PurchaseCaravanTicketOutcome.InvalidDestination => new GameTurnPrompt.Reply(
                "That isn't a valid destination for this caravan."
            ),
            PurchaseCaravanTicketOutcome.TravelSuspended => new GameTurnPrompt.Reply(
                "The caravan has suspended passenger service until the weather improves."
            ),
            _ => new GameTurnPrompt.Reply("The caravan isn't here anymore."),
        };
    }
}
