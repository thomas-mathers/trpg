using System.Transactions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Exceptions;
using TRPG.Application.Common.Queries;
using TRPG.Application.Configuration;
using TRPG.Application.Inventory;
using TRPG.Application.Inventory.Commands;
using TRPG.Application.Inventory.Queries;
using TRPG.Application.Routing.Queries;
using TRPG.Data.ModuleContexts;
using TRPG.Domain;
using TRPG.Domain.Models;

namespace TRPG.Application.Caravans.Commands;

public class PurchaseCaravanTicketCommand
{
    public required Guid PlayerId { get; init; }
    public required Guid WorldId { get; init; }
    public required Guid CaravanId { get; init; }
    public required Guid DestinationLocationId { get; init; }
    public required Guid PlayerLocationId { get; init; }
    public required TimeSpan Playtime { get; init; }
}

public enum PurchaseCaravanTicketOutcome
{
    Purchased,
    InsufficientGold,
    CaravanNotPresent,
    InvalidDestination,
    AlreadyHoldsTicket,
}

public record PurchaseCaravanTicketResult(
    PurchaseCaravanTicketOutcome Outcome,
    int? GoldCharged = null
);

internal class PurchaseCaravanTicketCommandHandler(
    ICaravansDbContext caravansContext,
    IRoutingDbContext routingContext,
    IQueryHandler<ResolveRouteTravelerPositionQuery, RoutePosition?> resolveRouteTravelerPosition,
    IQueryHandler<GetGoldQuantityQuery, int> getGoldQuantity,
    ICommandHandler<RemoveGoldCommand> removeGold,
    IOptionsSnapshot<CaravanOptions> caravanOptions
) : ICommandHandler<PurchaseCaravanTicketCommand, PurchaseCaravanTicketResult>
{
    public async Task<PurchaseCaravanTicketResult> Handle(
        PurchaseCaravanTicketCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var traveler =
            await routingContext
                .RouteTravelers.AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == command.CaravanId, cancellationToken)
            ?? throw new EntityNotFoundException(nameof(RouteTraveler), command.CaravanId);

        var fare = await caravansContext
            .CaravanFares.AsNoTracking()
            .FirstAsync(f => f.RouteId == traveler.RouteId, cancellationToken);

        var stopLocationIds = await routingContext
            .RouteStops.AsNoTracking()
            .Where(s => s.RouteId == traveler.RouteId)
            .Select(s => s.LocationId)
            .ToArrayAsync(cancellationToken);

        if (
            command.DestinationLocationId == command.PlayerLocationId
            || !stopLocationIds.AsEnumerable().Contains(command.DestinationLocationId)
        )
        {
            return new PurchaseCaravanTicketResult(PurchaseCaravanTicketOutcome.InvalidDestination);
        }

        var position = await resolveRouteTravelerPosition.Handle(
            new ResolveRouteTravelerPositionQuery
            {
                RouteTravelerId = command.CaravanId,
                Playtime = command.Playtime,
                SpeedUnitsPerHour = caravanOptions.Value.SpeedUnitsPerHour,
            },
            cancellationToken
        );
        if (
            position is not RoutePosition.Lingering lingering
            || lingering.LocationId != command.PlayerLocationId
        )
        {
            return new PurchaseCaravanTicketResult(PurchaseCaravanTicketOutcome.CaravanNotPresent);
        }

        var hasTicket = await caravansContext
            .CaravanTickets.AsNoTracking()
            .AnyAsync(
                t => t.CreatureId == command.PlayerId && t.RouteTravelerId == command.CaravanId,
                cancellationToken
            );
        if (hasTicket)
        {
            return new PurchaseCaravanTicketResult(PurchaseCaravanTicketOutcome.AlreadyHoldsTicket);
        }

        var fee = fare.TicketFeeGold;
        var playerGold = await getGoldQuantity.Handle(
            new GetGoldQuantityQuery
            {
                Owner = new ItemOwnerReference(command.PlayerId, OwnerType.Creature),
            },
            cancellationToken
        );
        if (playerGold < fee)
        {
            return new PurchaseCaravanTicketResult(PurchaseCaravanTicketOutcome.InsufficientGold);
        }

        using var transaction = new TransactionScope(
            TransactionScopeOption.Required,
            TransactionScopeAsyncFlowOption.Enabled
        );

        // The ticket fee is a pure gold sink — the caravan has no owning NPC or workstation to
        // credit, unlike an innkeeper's room rate.
        await removeGold.Handle(
            new RemoveGoldCommand
            {
                Owner = new ItemOwnerReference(command.PlayerId, OwnerType.Creature),
                Amount = fee,
            },
            cancellationToken
        );

        caravansContext.CaravanTickets.Add(
            new CaravanTicket
            {
                WorldId = command.WorldId,
                RouteTravelerId = command.CaravanId,
                CreatureId = command.PlayerId,
                OriginStopLocationId = command.PlayerLocationId,
                DestinationLocationId = command.DestinationLocationId,
                PurchasedAtPlaytime = command.Playtime,
            }
        );
        await caravansContext.SaveChangesAsync(cancellationToken);

        transaction.Complete();

        return new PurchaseCaravanTicketResult(PurchaseCaravanTicketOutcome.Purchased, fee);
    }
}
