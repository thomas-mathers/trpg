using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Exceptions;
using TRPG.Application.Common.Queries;
using TRPG.Application.Props.Queries;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Inventory;

internal class TradeOfferValidator(
    IInventoryDbContext context,
    IQueryHandler<GetPropByIdQuery, Prop?> getPropById
)
{
    public async Task<ValidatedTradeOffer> Validate(
        Guid playerId,
        Guid workstationId,
        IReadOnlyList<ItemSelection> playerOffer,
        IReadOnlyList<ItemSelection> shopOffer,
        CancellationToken cancellationToken = default
    )
    {
        var prop = await getPropById.Handle(
            new GetPropByIdQuery { Id = workstationId },
            cancellationToken
        );
        var workstation =
            prop as Workstation
            ?? throw new EntityNotFoundException("Trade workstation", workstationId);

        var shopOwner = new ItemOwnerReference(workstationId, OwnerType.Workstation);

        var playerValue = await GetOfferValue(
            playerOffer,
            new(playerId, OwnerType.Creature),
            cancellationToken
        );

        var shopValue = await GetOfferValue(shopOffer, shopOwner, cancellationToken);

        return new ValidatedTradeOffer(
            shopOwner,
            workstation.AssignedCreatureId,
            playerValue,
            shopValue
        );
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
