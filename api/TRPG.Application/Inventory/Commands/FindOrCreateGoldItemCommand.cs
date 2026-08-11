using Microsoft.EntityFrameworkCore;
using TRPG.Data;
using TRPG.Data.Models;

namespace TRPG.Application.Inventory.Commands;

internal class FindOrCreateGoldItemCommand
{
    public required ItemOwnerReference Owner { get; init; }
    public required Guid WorldId { get; init; }
}

internal class FindOrCreateGoldItemCommandHandler(TrpgDbContext context)
{
    public async Task<Gold> Handle(
        FindOrCreateGoldItemCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var gold = await context
            .Items.OfType<Gold>()
            .FirstOrDefaultAsync(
                item =>
                    item.Ownership.OwnerId == command.Owner.Id
                    && item.Ownership.OwnerType == command.Owner.Type,
                cancellationToken
            );
        if (gold is not null)
        {
            return gold;
        }

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
        return gold;
    }
}
