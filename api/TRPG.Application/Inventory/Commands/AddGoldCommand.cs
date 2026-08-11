using Microsoft.EntityFrameworkCore;
using TRPG.Data;
using TRPG.Data.Models;

namespace TRPG.Application.Inventory.Commands;

internal class AddGoldCommand
{
    public required int Amount { get; init; }
    public required ItemOwnerReference Owner { get; init; }
    public required Guid WorldId { get; init; }
}

internal class AddGoldCommandHandler(TrpgDbContext context)
{
    public async Task Handle(AddGoldCommand command, CancellationToken cancellationToken = default)
    {
        var gold = await context
            .Items.OfType<Gold>()
            .FirstOrDefaultAsync(
                item =>
                    item.Ownership.OwnerId == command.Owner.Id
                    && item.Ownership.OwnerType == command.Owner.Type,
                cancellationToken
            );
        if (gold is null)
        {
            gold = new Gold
            {
                WorldId = command.WorldId,
                Name = "Gold",
                Ownership = new ItemOwnership
                {
                    OwnerId = command.Owner.Id,
                    OwnerType = command.Owner.Type,
                },
            };
            context.Items.Add(gold);
        }

        gold.Quantity += command.Amount;
    }
}
