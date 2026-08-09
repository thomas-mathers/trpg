using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Exceptions;
using TRPG.Application.Inventory;
using TRPG.Application.Inventory.Commands;
using TRPG.Contracts.Inventory.Requests;
using TRPG.Data;
using TRPG.Data.Models;

namespace TRPG.Application.Trading;

internal class ProposeTradeCommand
{
    public required Guid PlayerId { get; init; }
    public required Guid WorkstationId { get; init; }
    public required IReadOnlyList<ItemSelection> PlayerOffer { get; init; }
    public required IReadOnlyList<ItemSelection> ShopOffer { get; init; }
}

internal class CompleteTradeCommand
{
    public required Guid PlayerId { get; init; }
    public required Guid WorkstationId { get; init; }
    public required IReadOnlyList<ItemSelection> PlayerOffer { get; init; }
    public required IReadOnlyList<ItemSelection> ShopOffer { get; init; }
}

internal class ProposeTradeCommandHandler(
    TradeOfferValidator validator,
    TradeOfferEvaluator evaluator
)
{
    public async Task<TradeOutcome> Handle(
        ProposeTradeCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var validation = await validator.Validate(
            command.PlayerId,
            command.WorkstationId,
            command.PlayerOffer,
            command.ShopOffer,
            cancellationToken
        );
        return evaluator.Evaluate(validation);
    }
}

internal class CompleteTradeCommandHandler(
    TrpgDbContext context,
    TradeOfferValidator validator,
    TradeOfferEvaluator evaluator,
    InventoryTransferCommandHandler transfer
)
{
    public async Task Handle(
        CompleteTradeCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var validation = await validator.Validate(
            command.PlayerId,
            command.WorkstationId,
            command.PlayerOffer,
            command.ShopOffer,
            cancellationToken
        );
        if (evaluator.Evaluate(validation) == TradeOutcome.Rejected)
            throw new InvalidOperationException("Trade offer is no longer accepted.");
        await using var transaction = await context.Database.BeginTransactionAsync(
            cancellationToken
        );
        await transfer.Handle(
            new InventoryTransferCommand
            {
                From = new(command.PlayerId, OwnerType.Creature),
                To = validation.ShopOwner,
                Items = command.PlayerOffer,
            },
            cancellationToken
        );
        await transfer.Handle(
            new InventoryTransferCommand
            {
                From = validation.ShopOwner,
                To = new(command.PlayerId, OwnerType.Creature),
                Items = command.ShopOffer,
            },
            cancellationToken
        );
        await transaction.CommitAsync(cancellationToken);
    }
}

internal class TradeOfferValidator(TrpgDbContext context)
{
    public async Task<ValidatedTradeOffer> Validate(
        Guid playerId,
        Guid workstationId,
        IReadOnlyList<ItemSelection> playerOffer,
        IReadOnlyList<ItemSelection> shopOffer,
        CancellationToken cancellationToken
    )
    {
        var playerExists = await context.Creatures.AnyAsync(
            c => c.Id == playerId,
            cancellationToken
        );
        if (!playerExists)
            throw new EntityNotFoundException("Player", playerId);
        var workstationExists = await context
            .Props.OfType<Workstation>()
            .AnyAsync(w => w.Id == workstationId, cancellationToken);
        if (!workstationExists)
            throw new EntityNotFoundException("Trade workstation", workstationId);
        var shopOwner = new ItemOwnerReference(workstationId, OwnerType.Workstation);
        var playerValue = await GetOfferValue(
            playerOffer,
            new(playerId, OwnerType.Creature),
            cancellationToken
        );
        var shopValue = await GetOfferValue(shopOffer, shopOwner, cancellationToken);
        return new ValidatedTradeOffer(shopOwner, playerValue, shopValue);
    }

    private async Task<int> GetOfferValue(
        IReadOnlyList<ItemSelection> selections,
        ItemOwnerReference owner,
        CancellationToken cancellationToken
    )
    {
        var ids = selections.Select(x => x.ItemId).ToArray();
        var items = await context
            .Items.Where(i =>
                ids.Contains(i.Id)
                && i.Ownership.OwnerId == owner.Id
                && i.Ownership.OwnerType == owner.Type
            )
            .ToDictionaryAsync(i => i.Id, cancellationToken);
        return selections.Sum(s =>
            !items.TryGetValue(s.ItemId, out var item)
            || s.Quantity <= 0
            || s.Quantity > item.Quantity
                ? throw new InvalidOperationException("An offered item is no longer available.")
                : item.GoldValue * s.Quantity
        );
    }
}
