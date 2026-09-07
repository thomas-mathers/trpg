using TRPG.Application.Common.Commands;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Inventory.Commands;

public class RestockGoldCommand
{
    public required ItemOwnerReference Owner { get; init; }
    public required Guid WorldId { get; init; }
    public required int MinimumQuantity { get; init; }
}

// Keyed on the owner, the way the one-gold-row-per-owner constraint is, so a row emptied by
// trading is topped back up rather than duplicated.
internal class RestockGoldCommandHandler(IInventoryDbContext context, GoldLoader goldLoader)
    : ICommandHandler<RestockGoldCommand>
{
    public async Task Handle(
        RestockGoldCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var gold = await goldLoader.FindGold(command.Owner, cancellationToken);
        if (gold is null)
        {
            context.Items.Add(
                new Gold
                {
                    WorldId = command.WorldId,
                    Name = "Gold",
                    Quantity = command.MinimumQuantity,
                    Ownership = new ItemOwnership
                    {
                        OwnerId = command.Owner.Id,
                        OwnerType = command.Owner.Type,
                    },
                }
            );
        }
        else if (gold.Quantity < command.MinimumQuantity)
        {
            gold.Quantity = command.MinimumQuantity;
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}
