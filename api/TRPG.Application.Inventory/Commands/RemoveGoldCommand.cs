using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Commands;
using TRPG.Data;
using TRPG.Domain.Models;

namespace TRPG.Application.Inventory.Commands;

public class RemoveGoldCommand
{
    public required ItemOwnerReference Owner { get; init; }
    public required int Amount { get; init; }
}

internal class RemoveGoldCommandHandler(TrpgDbContext context) : ICommandHandler<RemoveGoldCommand>
{
    public async Task Handle(RemoveGoldCommand command, CancellationToken cancellationToken = default)
    {
        if (command.Amount <= 0)
        {
            throw new InvalidOperationException("Gold amount must be positive.");
        }

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
            return;
        }

        gold.Quantity = Math.Max(0, gold.Quantity - command.Amount);
        await context.SaveChangesAsync(cancellationToken);
    }
}
