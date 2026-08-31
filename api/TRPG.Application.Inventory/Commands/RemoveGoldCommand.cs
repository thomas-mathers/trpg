using TRPG.Application.Common.Commands;
using TRPG.Data;

namespace TRPG.Application.Inventory.Commands;

public class RemoveGoldCommand
{
    public required ItemOwnerReference Owner { get; init; }
    public required int Amount { get; init; }
}

internal class RemoveGoldCommandHandler(TrpgDbContext context, GoldLoader goldLoader)
    : ICommandHandler<RemoveGoldCommand>
{
    public async Task Handle(
        RemoveGoldCommand command,
        CancellationToken cancellationToken = default
    )
    {
        if (command.Amount <= 0)
        {
            throw new InvalidOperationException("Gold amount must be positive.");
        }

        var gold = await goldLoader.FindGold(command.Owner, cancellationToken);
        if (gold is null || gold.Quantity < command.Amount)
        {
            throw new InvalidOperationException("Not enough gold to remove the requested amount.");
        }

        gold.Quantity -= command.Amount;
        await context.SaveChangesAsync(cancellationToken);
    }
}
