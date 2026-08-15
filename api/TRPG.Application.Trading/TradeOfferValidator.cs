using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Exceptions;
using TRPG.Application.Inventory;
using TRPG.Contracts.Inventory.Requests;
using TRPG.Data;
using TRPG.Data.Models;

namespace TRPG.Application.Trading;

internal class TradeOfferValidator(TrpgDbContext context)
{
    public async Task<ValidatedTradeOffer> Validate(
        Guid playerId,
        Guid workstationId,
        IReadOnlyList<ItemSelection> playerOffer,
        IReadOnlyList<ItemSelection> shopOffer,
        CancellationToken cancellationToken = default
    )
    {
        var playerExists = await context.Creatures.AnyAsync(
            creature => creature.Id == playerId,
            cancellationToken
        );
        if (!playerExists)
            throw new EntityNotFoundException("Player", playerId);

        var workstationExists = await context
            .Props.OfType<Workstation>()
            .AnyAsync(workstation => workstation.Id == workstationId, cancellationToken);

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
        var itemIds = selections.Select(selection => selection.ItemId).ToArray();

        var items = await context
            .Items.Where(item =>
                itemIds.AsEnumerable().Contains(item.Id)
                && item.Ownership.OwnerId == owner.Id
                && item.Ownership.OwnerType == owner.Type
            )
            .ToDictionaryAsync(item => item.Id, cancellationToken);

        return selections.Sum(selection =>
            !items.TryGetValue(selection.ItemId, out var item)
            || selection.Quantity <= 0
            || selection.Quantity > item.Quantity
                ? throw new InvalidOperationException("An offered item is no longer available.")
                : item.GoldValue * selection.Quantity
        );
    }
}
