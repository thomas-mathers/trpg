using System.Transactions;
using Microsoft.EntityFrameworkCore;
using TRPG.Application.Caravans.Queries;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Exceptions;
using TRPG.Application.Common.Queries;
using TRPG.Application.Inventory;
using TRPG.Application.Inventory.Commands;
using TRPG.Application.Inventory.Queries;
using TRPG.Data.ModuleContexts;
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
    ICaravansDbContext context,
    IQueryHandler<ResolveCaravanPositionQuery, CaravanPosition?> resolveCaravanPosition,
    IQueryHandler<GetGoldQuantityQuery, int> getGoldQuantity,
    ICommandHandler<RemoveGoldCommand> removeGold
) : ICommandHandler<PurchaseCaravanTicketCommand, PurchaseCaravanTicketResult>
{
    public async Task<PurchaseCaravanTicketResult> Handle(
        PurchaseCaravanTicketCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var caravan =
            await context
                .Caravans.AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == command.CaravanId, cancellationToken)
            ?? throw new EntityNotFoundException(nameof(Caravan), command.CaravanId);

        var route = await context
            .CaravanRoutes.AsNoTracking()
            .FirstAsync(r => r.Id == caravan.CaravanRouteId, cancellationToken);

        var stopLocationIds = await context
            .CaravanRouteStops.AsNoTracking()
            .Where(s => s.CaravanRouteId == caravan.CaravanRouteId)
            .Select(s => s.LocationId)
            .ToArrayAsync(cancellationToken);

        if (
            command.DestinationLocationId == command.PlayerLocationId
            || !stopLocationIds.AsEnumerable().Contains(command.DestinationLocationId)
        )
        {
            return new PurchaseCaravanTicketResult(PurchaseCaravanTicketOutcome.InvalidDestination);
        }

        var position = await resolveCaravanPosition.Handle(
            new ResolveCaravanPositionQuery
            {
                CaravanId = command.CaravanId,
                Playtime = command.Playtime,
            },
            cancellationToken
        );
        if (
            position is not CaravanPosition.Lingering lingering
            || lingering.LocationId != command.PlayerLocationId
        )
        {
            return new PurchaseCaravanTicketResult(PurchaseCaravanTicketOutcome.CaravanNotPresent);
        }

        var hasTicket = await context
            .CaravanTickets.AsNoTracking()
            .AnyAsync(
                t => t.CreatureId == command.PlayerId && t.CaravanId == command.CaravanId,
                cancellationToken
            );
        if (hasTicket)
        {
            return new PurchaseCaravanTicketResult(PurchaseCaravanTicketOutcome.AlreadyHoldsTicket);
        }

        var fee = route.TicketFeeGold;
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

        context.CaravanTickets.Add(
            new CaravanTicket
            {
                WorldId = command.WorldId,
                CaravanId = command.CaravanId,
                CreatureId = command.PlayerId,
                OriginStopLocationId = command.PlayerLocationId,
                DestinationLocationId = command.DestinationLocationId,
                PurchasedAtPlaytime = command.Playtime,
            }
        );
        await context.SaveChangesAsync(cancellationToken);

        transaction.Complete();

        return new PurchaseCaravanTicketResult(PurchaseCaravanTicketOutcome.Purchased, fee);
    }
}
